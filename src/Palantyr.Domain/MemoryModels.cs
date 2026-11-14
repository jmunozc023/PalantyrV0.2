using Palantyr.Contracts;

namespace Palantyr.Domain;

/// <summary>
/// Embedded state or belief history entry carrying its own item-level VisibilityScope (ADR-004, ADR-006, Kaede Finding 3).
/// </summary>
public sealed record MemoryHistoryItem(
    string HistoryItemId,
    string Description,
    KnowledgeState KnowledgeState,
    ConfidenceLevel Confidence,
    VisibilityScope Visibility,
    string? ValidFromSessionId,
    string? ValidToSessionId,
    string? RelativeTimeContext,
    string? SupersededByHistoryItemId,
    DateTimeOffset RecordedAtUtc,
    IReadOnlyList<string>? AuthorizedCharacterIds = null);

/// <summary>
/// Universal Memory Document persisted in RavenDB per campaign (ADR-004).
/// </summary>
public sealed class MemoryEntityDocument
{
    public required string Id { get; init; }
    public required MemoryEntityType EntityType { get; init; }
    public required string CanonicalName { get; set; }
    public List<string> Aliases { get; init; } = [];
    public List<string> Titles { get; init; } = [];
    public List<string> RecognitionVariants { get; init; } = [];
    public string SummaryDescription { get; set; } = string.Empty;
    public string? DmPrivateNotes { get; set; }
    public VisibilityScope Visibility { get; set; } = VisibilityScope.PartyKnown;
    public List<string> AuthorizedCharacterIds { get; init; } = [];
    public KnowledgeState KnowledgeState { get; set; } = KnowledgeState.CampaignTruth;
    public ConfidenceLevel Confidence { get; set; } = ConfidenceLevel.HighConfidence;
    public double InternalConfidenceScore { get; set; } = 0.85;
    public FirstAppearanceRef? FirstAppearance { get; set; }
    public List<MemoryHistoryItem> StateAndBeliefHistory { get; init; } = [];
    public List<ProvenanceRef> Sources { get; init; } = [];
    public ProcessingLineage? Lineage { get; set; }

    /// <summary>
    /// Projects a Party-Safe or Character-Scoped view BEFORE any prompt or UI consumer sees the object (ADR-006, Kaede Finding 3).
    /// </summary>
    public PartySafeMemoryView? ToPartySafeView(QueryVisibilityContext context)
    {
        if (!context.Permits(Visibility, AuthorizedCharacterIds))
        {
            return null;
        }

        if (!context.IncludeDmPrivate && KnowledgeState == KnowledgeState.DMPrivateTruth)
        {
            return null;
        }

        var allowedHistory = StateAndBeliefHistory
            .Where(h => context.Permits(h.Visibility, h.AuthorizedCharacterIds)
                        && (context.IncludeDmPrivate || h.KnowledgeState != KnowledgeState.DMPrivateTruth))
            .ToList();

        return new PartySafeMemoryView(
            Id: Id,
            EntityType: EntityType,
            CanonicalName: CanonicalName,
            Aliases: Aliases.ToList(),
            Titles: Titles.ToList(),
            SummaryDescription: SummaryDescription,
            Visibility: Visibility,
            KnowledgeState: KnowledgeState,
            Confidence: Confidence,
            FirstAppearance: FirstAppearance,
            StateAndBeliefHistory: allowedHistory,
            Sources: Sources.ToList());
    }
}

/// <summary>
/// Stripped projection guaranteed free of DMPrivate notes, RecognitionVariants, and unauthorized sub-items (ADR-006, Kaede Finding 3).
/// </summary>
public sealed record PartySafeMemoryView(
    string Id,
    MemoryEntityType EntityType,
    string CanonicalName,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> Titles,
    string SummaryDescription,
    VisibilityScope Visibility,
    KnowledgeState KnowledgeState,
    ConfidenceLevel Confidence,
    FirstAppearanceRef? FirstAppearance,
    IReadOnlyList<MemoryHistoryItem> StateAndBeliefHistory,
    IReadOnlyList<ProvenanceRef> Sources);

/// <summary>
/// Model-upgrade-resilient rejection & resolution tombstone (ADR-004, Kaede Finding 4).
/// </summary>
public sealed record MemoryResolutionTombstone(
    string Id,
    string SessionId,
    string SegmentId,
    long ApproximateStartMs,
    long ApproximateEndMs,
    string NormalizedFactSignature,
    IReadOnlyList<string> TargetEntityIds,
    string DmDecision,
    DateTimeOffset ResolvedAtUtc)
{
    public bool MatchesOverlap(string sessionId, string segmentId, long spanStartMs, long spanEndMs, string normalizedSignature, long toleranceMs = 2500)
    {
        if (!string.Equals(SessionId, sessionId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(SegmentId, segmentId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(NormalizedFactSignature, normalizedSignature, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        long expandedStart = Math.Max(0, ApproximateStartMs - toleranceMs);
        long expandedEnd = ApproximateEndMs + toleranceMs;
        return spanStartMs <= expandedEnd && spanEndMs >= expandedStart;
    }
}
