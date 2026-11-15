namespace Palantyr.Contracts;

/// <summary>
/// Status of an entity candidate in the "Resolve ASAP" queue (Sec 8, US-06.5).
/// </summary>
public enum EntityResolutionStatus
{
    PendingDmReview = 0,
    LinkedToExisting = 1,
    CreatedNewEntity = 2,
    Dismissed = 3
}

/// <summary>
/// Metric/heuristic that triggered a candidate match proposal (US-06.5, FR-MEM-ENTITY-002).
/// </summary>
public enum ResolutionMatchReason
{
    ExactAliasMatch = 0,
    PhoneticSimilarity = 1,
    LevenshteinDistance = 2,
    KnownSpeechVariant = 3
}

/// <summary>
/// Candidate entity proposed for resolution (US-06.5).
/// </summary>
public sealed record CandidateMatchDto(
    string EntityId,
    string CanonicalName,
    MemoryEntityType EntityType,
    double MatchScore,
    ResolutionMatchReason MatchReason);

/// <summary>
/// Entity resolution candidate item stored in the "Resolve ASAP" queue (Sec 8, US-06.5).
/// </summary>
public sealed record EntityResolutionCandidateDto(
    string CandidateId,
    string CampaignId,
    string ObservedSurfaceName,
    string? ContextSnippet,
    string? SourceSessionId,
    string? SourceSegmentId,
    string? SourceSpanId,
    DateTimeOffset DetectedAtUtc,
    EntityResolutionStatus Status,
    IReadOnlyList<CandidateMatchDto> PotentialMatches,
    string? ResolvedEntityId = null,
    string? ResolutionDecision = null);

/// <summary>
/// DM ruling request payload to resolve an ambiguous entity candidate (US-06.5, FR-VAL-001).
/// </summary>
public sealed record ResolveCandidateRequest(
    string Action, // "LinkToExisting" | "CreateNew" | "AcousticCorrection" | "Dismiss"
    string? TargetEntityId = null,
    string? NewEntityCanonicalName = null,
    bool RegisterAsSpeechVariant = false,
    string? DmNotes = null,
    string DecidedBy = "DM");

/// <summary>
/// Characterization of divergence between multiple memory claims (Sec 12, US-06.6).
/// </summary>
public enum ConflictType
{
    FactualContradiction = 0,
    TemporalEvolution = 1,
    DivergentBelief = 2
}

/// <summary>
/// Resolution status of a memory conflict (US-06.6).
/// </summary>
public enum ConflictResolutionState
{
    Unresolved = 0,
    ResolvedSuperseded = 1,
    ResolvedCoexistingBeliefs = 2,
    ResolvedDmRuling = 3
}

/// <summary>
/// Individual competing assertion in a memory conflict (US-06.6, FR-MEM-CONFLICT-002).
/// </summary>
public sealed record CompetingAssertionDto(
    string AssertionId,
    string AssertionText,
    ProvenanceRef? SourceRef,
    KnowledgeState KnowledgeState,
    ConfidenceLevel Confidence,
    DateTimeOffset RecordedAtUtc);

/// <summary>
/// Conflict record tracking competing assertions without deleting evidence (Sec 12, US-06.6).
/// </summary>
public sealed record MemoryConflictDto(
    string ConflictId,
    string CampaignId,
    string EntityId,
    ConflictType ConflictType,
    string Summary,
    IReadOnlyList<CompetingAssertionDto> CompetingAssertions,
    ConflictResolutionState State,
    string? DmRuling = null,
    DateTimeOffset? ResolvedAtUtc = null);

/// <summary>
/// DM ruling payload to resolve a memory conflict (US-06.6, FR-MEM-CONFLICT-004).
/// </summary>
public sealed record ResolveConflictRequest(
    ConflictResolutionState Resolution,
    string? WinningAssertionId = null,
    string? DmRulingNote = null,
    string DecidedBy = "DM");
