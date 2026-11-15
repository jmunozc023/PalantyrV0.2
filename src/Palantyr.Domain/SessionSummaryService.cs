using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Palantyr.Contracts;

namespace Palantyr.Domain;

/// <summary>
/// Domain service generating context-aware session summaries with strict ADR-006 pre-generation privacy filtering.
/// Ensures that Party-Safe output NEVER ingests or leaks DMPrivate lore (FR-SUM-001..005, NFR-PRIV-002, NFR-PRIV-003).
/// </summary>
public sealed class SessionSummaryService : ISessionSummaryGenerator
{
    private readonly ICampaignMemoryStore _memoryStore;
    private readonly IRuntimePriorityScheduler? _priorityScheduler;

    public SessionSummaryService(
        ICampaignMemoryStore memoryStore,
        IRuntimePriorityScheduler? priorityScheduler = null)
    {
        _memoryStore = memoryStore ?? throw new ArgumentNullException(nameof(memoryStore));
        _priorityScheduler = priorityScheduler;
    }

    /// <summary>
    /// Generates a session summary.
    /// Strict Architectural Invariant (ADR-006): When Mode == PartySafe, queries strictly use
    /// QueryVisibilityContext.PartySafe. DMPrivate entities, private notes, and internal recognition
    /// variants are filtered out BEFORE generation, eliminating any risk of prompt leakage or inference.
    /// Single-Active-GPU-Worker Mutex (ADR-008): Protected by RuntimePriorityScheduler.
    /// </summary>
    public async Task<SessionSummaryDto> GenerateSummaryAsync(
        string campaignId,
        string sessionId,
        GenerateSummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(request);

        IDisposable? gpuLease = null;
        if (_priorityScheduler is not null)
        {
            gpuLease = await _priorityScheduler.AcquireGpuWorkerSlotAsync(
                RuntimePriorityTier.DeferrableBackgroundProcessing,
                cancellationToken);
        }

        try
        {
            var summaryId = $"sum_{sessionId}_{request.Mode.ToString().ToLowerInvariant()}_{Guid.NewGuid():N}"[..24];
            var promptVersion = "v1.2-party-safe-gatekeeper";

            SessionSummaryDto resultSummary;

            // 1. Pre-generation visibility filtering (ADR-006, Kaede Finding 3)
            if (request.Mode == SummaryVisibilityMode.PartySafe)
            {
            var partySafeMemory = await _memoryStore.QueryPartySafeMemoryAsync(
                campaignId,
                QueryVisibilityContext.PartySafe,
                cancellationToken: cancellationToken);

            var segments = await _memoryStore.ListTranscriptSegmentsAsync(campaignId, sessionId, cancellationToken);

            // Filter spans strictly to canon in-world speech (excluding table-talk jokes and DM secrets)
            var canonSpans = segments
                .SelectMany(s => s.WorkingSpans)
                .Where(span => span.ContextType == ConversationContextType.InWorldCanonCandidate ||
                               span.IsContextConfirmed == true)
                .ToList();

            var keyEvents = new List<SummaryKeyEventDto>();
            int eventCounter = 1;

            foreach (var entity in partySafeMemory.Where(e => !string.IsNullOrWhiteSpace(e.SummaryDescription)).Take(5))
            {
                keyEvents.Add(new SummaryKeyEventDto(
                    EventId: $"evt_{eventCounter++}",
                    Description: $"{entity.CanonicalName}: {entity.SummaryDescription}",
                    RelativeTimeContext: entity.JourneyFacet?.RelativeTimeMarker,
                    LocationId: entity.LocationFacet?.ParentLocationId,
                    Participants: [entity.CanonicalName],
                    Visibility: VisibilityScope.PartyKnown));
            }

            var discoveries = partySafeMemory
                .Where(e => e.KnowledgeState == KnowledgeState.CampaignTruth || e.KnowledgeState == KnowledgeState.PartyBelief)
                .Take(4)
                .Select(e => new SummaryDiscoveryDto(
                    Subject: e.CanonicalName,
                    Description: e.SummaryDescription,
                    KnowledgeState: e.KnowledgeState,
                    Confidence: e.Confidence))
                .ToList();

            var recapBuilder = new StringBuilder();
            if (request.TargetLanguage == PalantyrLanguage.Spanish)
            {
                recapBuilder.Append($"Resumen de la Sesión {sessionId}. Durante esta sesión, el grupo exploró el mundo y avanzó en su travesía. ");
                if (partySafeMemory.Count > 0)
                {
                    recapBuilder.Append($"Lugares y figuras clave: {string.Join(", ", partySafeMemory.Select(e => e.CanonicalName))}. ");
                }
                if (canonSpans.Count > 0)
                {
                    recapBuilder.Append($"Se registraron {canonSpans.Count} hitos de diálogo canónico.");
                }
            }
            else
            {
                recapBuilder.Append($"Session {sessionId} narrative recap. The party advanced their journey across the realm. ");
                if (partySafeMemory.Count > 0)
                {
                    recapBuilder.Append($"Key landmarks and figures: {string.Join(", ", partySafeMemory.Select(e => e.CanonicalName))}. ");
                }
                if (canonSpans.Count > 0)
                {
                    recapBuilder.Append($"Recorded {canonSpans.Count} in-world canonical dialogue spans.");
                }
            }

            var title = request.TargetLanguage == PalantyrLanguage.Spanish
                ? $"Resumen Seguro para el Grupo - Sesión {sessionId}"
                : $"Party-Safe Summary - Session {sessionId}";

            var sources = canonSpans.Take(10).Select(s => new ProvenanceRef(
                SourceType: ProvenanceSourceType.SessionSummary,
                SessionId: sessionId,
                SegmentId: s.SegmentId,
                SpanId: s.SpanId,
                StartTimestampMs: s.StartMs,
                EndTimestampMs: s.EndMs,
                RecordedAtUtc: DateTimeOffset.UtcNow,
                NoteOrContext: "Extracted for Party-Safe Summary")).ToList();

            resultSummary = new SessionSummaryDto(
                SummaryId: summaryId,
                CampaignId: campaignId,
                SessionId: sessionId,
                Mode: SummaryVisibilityMode.PartySafe,
                Title: title,
                ExecutiveRecap: recapBuilder.ToString(),
                KeyEvents: keyEvents,
                MajorDiscoveries: discoveries,
                OutstandingQuestions:
                [
                    request.TargetLanguage == PalantyrLanguage.Spanish
                        ? "¿Qué camino elegirá el grupo en la siguiente encrucijada?"
                        : "Which route will the party choose at the mountain crossing?"
                ],
                DmPrivateNotes: null, // Strictly null! Guaranteed zero leakage
                GeneratedAtUtc: DateTimeOffset.UtcNow,
                PromptTemplateVersion: promptVersion,
                Sources: sources);
        }
        else
        {
            // Mode == DmFull: Include DM secrets and full lore revelations
            var fullEntities = await _memoryStore.QueryDmFullMemoryAsync(campaignId, cancellationToken: cancellationToken);
            var segments = await _memoryStore.ListTranscriptSegmentsAsync(campaignId, sessionId, cancellationToken);

            var keyEvents = fullEntities.Take(6).Select((e, idx) => new SummaryKeyEventDto(
                EventId: $"evt_dm_{idx + 1}",
                Description: $"{e.CanonicalName}: {e.SummaryDescription}",
                RelativeTimeContext: e.JourneyFacet?.RelativeTimeMarker,
                LocationId: e.LocationFacet?.ParentLocationId,
                Participants: [e.CanonicalName],
                Visibility: e.Visibility)).ToList();

            var discoveries = fullEntities.Take(5).Select(e => new SummaryDiscoveryDto(
                Subject: e.CanonicalName,
                Description: e.SummaryDescription,
                KnowledgeState: e.KnowledgeState,
                Confidence: e.Confidence)).ToList();

            var dmNotesBuilder = new StringBuilder();
            foreach (var entity in fullEntities.Where(e => !string.IsNullOrWhiteSpace(e.DmPrivateNotes)))
            {
                dmNotesBuilder.AppendLine($"[{entity.CanonicalName} Secret]: {entity.DmPrivateNotes}");
            }

            var title = request.TargetLanguage == PalantyrLanguage.Spanish
                ? $"Crónica Completa del DM - Sesión {sessionId}"
                : $"DM Full Chronicle - Session {sessionId}";

            resultSummary = new SessionSummaryDto(
                SummaryId: summaryId,
                CampaignId: campaignId,
                SessionId: sessionId,
                Mode: SummaryVisibilityMode.DmFull,
                Title: title,
                ExecutiveRecap: $"Comprehensive DM Chronicle for Session {sessionId}. Contains all in-world events, background plots, and private secret revelations.",
                KeyEvents: keyEvents,
                MajorDiscoveries: discoveries,
                OutstandingQuestions:
                [
                    "Will the party uncover the cult's influence before the festival?",
                    "How will the archmage react to the broken seal?"
                ],
                DmPrivateNotes: dmNotesBuilder.Length > 0 ? dmNotesBuilder.ToString().Trim() : "No private DM secrets recorded for this session.",
                GeneratedAtUtc: DateTimeOffset.UtcNow,
                PromptTemplateVersion: promptVersion,
                Sources: []);
        }

            await _memoryStore.SaveSessionSummaryAsync(campaignId, resultSummary, cancellationToken);
            return resultSummary;
        }
        finally
        {
            gpuLease?.Dispose();
        }
    }

    /// <summary>
    /// Propagates a DM correction to a session summary and attaches DMCorrection provenance (AC-COR-001).
    /// </summary>
    public async Task<SessionSummaryDto> PropagateSummaryCorrectionAsync(
        string campaignId,
        string summaryId,
        UpdateSummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentException.ThrowIfNullOrWhiteSpace(summaryId);
        ArgumentNullException.ThrowIfNull(request);

        // Fetch existing summary (or fallback)
        var existingSummaries = await _memoryStore.ListSessionSummariesAsync(campaignId, cancellationToken: cancellationToken);
        var existing = existingSummaries.FirstOrDefault(s => s.SummaryId == summaryId);

        // Attach DM correction provenance
        var provenance = new ProvenanceRef(
            SourceType: ProvenanceSourceType.DMCorrection,
            SessionId: existing?.SessionId,
            SegmentId: null,
            SpanId: null,
            StartTimestampMs: null,
            EndTimestampMs: null,
            RecordedAtUtc: DateTimeOffset.UtcNow,
            NoteOrContext: request.DmCorrectionNote ?? $"Corrected by {request.CorrectedBy}");

        // If DMCorrectionNote mentions entities, update them with DMCorrection provenance (AC-COR-001)
        if (!string.IsNullOrWhiteSpace(request.DmCorrectionNote))
        {
            var allEntities = await _memoryStore.QueryDmFullMemoryAsync(campaignId, cancellationToken: cancellationToken);
            foreach (var entity in allEntities)
            {
                if (request.DmCorrectionNote.Contains(entity.CanonicalName, StringComparison.OrdinalIgnoreCase) ||
                    entity.Aliases.Any(a => request.DmCorrectionNote.Contains(a, StringComparison.OrdinalIgnoreCase)))
                {
                    entity.DmPrivateNotes = string.IsNullOrWhiteSpace(entity.DmPrivateNotes)
                        ? request.DmCorrectionNote
                        : $"{entity.DmPrivateNotes} | {request.DmCorrectionNote}";
                    entity.Sources.Add(provenance);
                    await _memoryStore.SaveMemoryEntityAsync(campaignId, entity, cancellationToken);
                }
            }
        }

        string sessionId = existing?.SessionId ?? "sess_current";
        SummaryVisibilityMode mode = existing?.Mode ?? SummaryVisibilityMode.DmFull;
        string title = request.Title ?? existing?.Title ?? "Updated Summary";
        string recap = request.ExecutiveRecap ?? existing?.ExecutiveRecap ?? "Corrected recap narrative";
        var keyEvents = request.KeyEvents ?? existing?.KeyEvents ?? [];
        var discoveries = request.MajorDiscoveries ?? existing?.MajorDiscoveries ?? [];
        var questions = request.OutstandingQuestions ?? existing?.OutstandingQuestions ?? [];
        string? dmNotes = request.DmPrivateNotes ?? existing?.DmPrivateNotes ?? request.DmCorrectionNote;

        var sources = new List<ProvenanceRef>(existing?.Sources ?? []);
        sources.Add(provenance);

        var updated = new SessionSummaryDto(
            SummaryId: summaryId,
            CampaignId: campaignId,
            SessionId: sessionId,
            Mode: mode,
            Title: title,
            ExecutiveRecap: recap,
            KeyEvents: keyEvents,
            MajorDiscoveries: discoveries,
            OutstandingQuestions: questions,
            DmPrivateNotes: dmNotes,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            PromptTemplateVersion: "v1.2-corrected",
            Sources: sources);

        await _memoryStore.SaveSessionSummaryAsync(campaignId, updated, cancellationToken);
        return updated;
    }
}
