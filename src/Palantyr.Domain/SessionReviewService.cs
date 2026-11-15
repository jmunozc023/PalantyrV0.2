using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Palantyr.Contracts;

namespace Palantyr.Domain;

/// <summary>
/// Domain service orchestrating the post-session review workflow, conversational Needs Attention resolution,
/// 10-second audio context verification, and batch Full Review with immediate autosave and Undo (EPIC-09, US-09.1..09.5).
/// </summary>
public sealed class SessionReviewService
{
    private readonly ICampaignMemoryStore _memoryStore;
    private readonly ConcurrentDictionary<string, List<MemoryEntityDocument>> _undoSnapshots = new();

    public SessionReviewService(ICampaignMemoryStore memoryStore)
    {
        _memoryStore = memoryStore ?? throw new ArgumentNullException(nameof(memoryStore));
    }

    /// <summary>
    /// Resolves an individual Needs Attention item ("one scroll at a time") via natural language or quick buttons (US-09.2, AC-REV-001).
    /// </summary>
    public async Task<bool> ResolveAttentionItemAsync(
        string campaignId,
        string itemId,
        ResolveAttentionItemRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        ArgumentNullException.ThrowIfNull(request);

        string decisionSummary = request.Action switch
        {
            NeedsAttentionAction.Confirm => $"Confirmed by {request.ResolvedBy}: {request.CorrectionText ?? "Accepted as Canon"}",
            NeedsAttentionAction.RejectWithTombstone => $"Rejected by {request.ResolvedBy}: {request.Notes ?? "Not real in-world lore"}",
            NeedsAttentionAction.DeferOrUnresolved => $"Deferred by {request.ResolvedBy}",
            NeedsAttentionAction.LinkToExisting => $"Linked to {request.TargetEntityId} by {request.ResolvedBy}",
            NeedsAttentionAction.ClassifyAsJoke => $"Classified as table-talk joke by {request.ResolvedBy}",
            NeedsAttentionAction.PromoteToCanon => $"Promoted from joke to canon by {request.ResolvedBy}",
            _ => $"Resolved by {request.ResolvedBy}"
        };

        // 1. Resolve item in store
        await _memoryStore.ResolveNeedsAttentionItemAsync(campaignId, itemId, decisionSummary, cancellationToken);

        // 2. If rejected with tombstone (ADR-004, Kaede Finding 4), persist a resolution tombstone
        if (request.Action == NeedsAttentionAction.RejectWithTombstone && !string.IsNullOrWhiteSpace(request.CorrectionText))
        {
            var tombstone = new EntityResolutionCandidateDto(
                CandidateId: $"tombstone_{itemId}",
                CampaignId: campaignId,
                ObservedSurfaceName: request.CorrectionText,
                ContextSnippet: request.Notes ?? "Rejected lore",
                SourceSessionId: null,
                SourceSegmentId: null,
                SourceSpanId: null,
                DetectedAtUtc: DateTimeOffset.UtcNow,
                Status: EntityResolutionStatus.Dismissed,
                PotentialMatches: [],
                ResolvedEntityId: null,
                ResolutionDecision: $"Tombstone persisted by {request.ResolvedBy}: rejected hallucination/joke");
            await _memoryStore.SaveResolutionCandidateAsync(campaignId, tombstone, cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// Processes a batch of Full Review confirmations or rejections with immediate autosave and Undo tracking (US-09.4, US-09.5, AC-REV-002).
    /// </summary>
    public async Task<ReviewUndoToken> ProcessBatchReviewAsync(
        string campaignId,
        BatchReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentNullException.ThrowIfNull(request);

        var undoTokenId = $"undo_{request.SessionId}_{Guid.NewGuid():N}"[..20];
        var affectedEntityIds = new List<string>();
        var originalSnapshots = new List<MemoryEntityDocument>();

        foreach (var decision in request.Decisions)
        {
            var entity = await _memoryStore.GetMemoryEntityAsync(campaignId, decision.EntityId, cancellationToken);
            if (entity is null) continue;

            // Take deep snapshot for Undo
            originalSnapshots.Add(CloneEntity(entity));
            affectedEntityIds.Add(entity.Id);

            if (decision.IsConfirmed)
            {
                // Promote to DM Confirmed (FR-REV-002)
                entity.Confidence = ConfidenceLevel.DMConfirmed;
                entity.KnowledgeState = KnowledgeState.CampaignTruth;
                entity.Sources.Add(new ProvenanceRef(
                    SourceType: ProvenanceSourceType.DMCorrection,
                    SessionId: request.SessionId,
                    SegmentId: null,
                    SpanId: null,
                    StartTimestampMs: null,
                    EndTimestampMs: null,
                    RecordedAtUtc: DateTimeOffset.UtcNow,
                    NoteOrContext: $"Confirmed during Full Review by {request.DecidedBy}"));
            }
            else if (decision.IsRejected)
            {
                // Mark superseded/false rather than silently deleting (FR-REV-014)
                entity.KnowledgeState = KnowledgeState.DisprovenOrSupersededBelief;
                entity.Sources.Add(new ProvenanceRef(
                    SourceType: ProvenanceSourceType.DMCorrection,
                    SessionId: request.SessionId,
                    SegmentId: null,
                    SpanId: null,
                    StartTimestampMs: null,
                    EndTimestampMs: null,
                    RecordedAtUtc: DateTimeOffset.UtcNow,
                    NoteOrContext: $"Marked false/rejected during Full Review: {decision.RejectionReason ?? "Rejected by DM"}"));
            }

            if (!string.IsNullOrWhiteSpace(decision.CorrectionText))
            {
                entity.SummaryDescription = decision.CorrectionText;
            }

            // Immediate autosave per decision (FR-REV-003)
            await _memoryStore.SaveMemoryEntityAsync(campaignId, entity, cancellationToken);
        }

        // Store snapshot for Undo
        // Bounded Undo cache (max 20 tokens) to prevent unbounded memory growth
        while (_undoSnapshots.Count >= 20)
        {
            var oldestKey = _undoSnapshots.Keys.FirstOrDefault();
            if (oldestKey is not null)
            {
                _undoSnapshots.TryRemove(oldestKey, out _);
            }
            else
            {
                break;
            }
        }

        _undoSnapshots[undoTokenId] = originalSnapshots;

        return new ReviewUndoToken(
            UndoTokenId: undoTokenId,
            CampaignId: campaignId,
            SessionId: request.SessionId,
            ExecutedAtUtc: DateTimeOffset.UtcNow,
            AffectedEntityIds: affectedEntityIds);
    }

    /// <summary>
    /// Executes an immediate Undo on a recent batch review action (US-09.5, NFR-USAB-008).
    /// </summary>
    public async Task<bool> UndoBatchReviewAsync(
        string campaignId,
        string undoTokenId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentException.ThrowIfNullOrWhiteSpace(undoTokenId);

        if (!_undoSnapshots.TryRemove(undoTokenId, out var previousStates))
        {
            return false;
        }

        foreach (var originalEntity in previousStates)
        {
            await _memoryStore.SaveMemoryEntityAsync(campaignId, originalEntity, cancellationToken);
        }

        return true;
    }

    private static MemoryEntityDocument CloneEntity(MemoryEntityDocument e)
    {
        return new MemoryEntityDocument
        {
            Id = e.Id,
            EntityType = e.EntityType,
            CanonicalName = e.CanonicalName,
            Aliases = new List<string>(e.Aliases),
            Titles = new List<string>(e.Titles),
            RecognitionVariants = new List<string>(e.RecognitionVariants),
            SummaryDescription = e.SummaryDescription,
            DmPrivateNotes = e.DmPrivateNotes,
            Visibility = e.Visibility,
            AuthorizedCharacterIds = e.AuthorizedCharacterIds is not null ? new List<string>(e.AuthorizedCharacterIds) : null,
            KnowledgeState = e.KnowledgeState,
            Confidence = e.Confidence,
            FirstAppearance = e.FirstAppearance,
            CharacterFacet = e.CharacterFacet is null ? null : new CharacterFacet
            {
                RaceOrSpecies = e.CharacterFacet.RaceOrSpecies,
                PhysicalDescription = e.CharacterFacet.PhysicalDescription,
                RoleOrArchetype = e.CharacterFacet.RoleOrArchetype,
                Affiliations = new List<string>(e.CharacterFacet.Affiliations),
                StatementsBy = new List<CharacterStatement>(e.CharacterFacet.StatementsBy),
                StatementsAbout = new List<CharacterStatement>(e.CharacterFacet.StatementsAbout)
            },
            LocationFacet = e.LocationFacet is null ? null : new LocationFacet
            {
                LocationType = e.LocationFacet.LocationType,
                ParentLocationId = e.LocationFacet.ParentLocationId,
                EnclosedChildIds = new List<string>(e.LocationFacet.EnclosedChildIds),
                IsTransitPoint = e.LocationFacet.IsTransitPoint,
                TransitEndpoints = e.LocationFacet.TransitEndpoints
            },
            JourneyFacet = e.JourneyFacet is null ? null : new JourneyFacet
            {
                ParentJourneyId = e.JourneyFacet.ParentJourneyId,
                OriginLocationId = e.JourneyFacet.OriginLocationId,
                DestinationLocationId = e.JourneyFacet.DestinationLocationId,
                TotalEstimatedDays = e.JourneyFacet.TotalEstimatedDays,
                CurrentRelativeDay = e.JourneyFacet.CurrentRelativeDay,
                RelativeTimeMarker = e.JourneyFacet.RelativeTimeMarker,
                DepartureCampaignDate = e.JourneyFacet.DepartureCampaignDate,
                ArrivalCampaignDate = e.JourneyFacet.ArrivalCampaignDate,
                DayTransitions = new List<RelativeDayTransition>(e.JourneyFacet.DayTransitions),
                KeyMoments = new List<KeyMomentRef>(e.JourneyFacet.KeyMoments)
            },
            StateAndBeliefHistory = new List<MemoryHistoryItem>(e.StateAndBeliefHistory),
            Sources = new List<ProvenanceRef>(e.Sources)
        };
    }
}
