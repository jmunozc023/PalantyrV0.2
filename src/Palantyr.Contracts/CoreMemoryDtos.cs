namespace Palantyr.Contracts;

/// <summary>
/// Statement by or about a character (FR-MEM-CHAR-001..002).
/// </summary>
public sealed record CharacterStatementDto(
    string StatementId,
    string SessionId,
    string? SegmentId,
    string? SpanId,
    long? TimestampMs,
    string SummaryText,
    string? TargetSubject,
    bool IsByCharacter,
    VisibilityScope Visibility = VisibilityScope.PartyKnown);

/// <summary>
/// Character facet DTO carrying character-specific domain attributes (Sec 9, US-06.2).
/// </summary>
public sealed record CharacterFacetDto(
    string? RaceOrSpecies,
    string? PhysicalDescription,
    string? RoleOrArchetype,
    IReadOnlyList<string> Affiliations,
    IReadOnlyList<CharacterStatementDto> StatementsBy,
    IReadOnlyList<CharacterStatementDto> StatementsAbout);

/// <summary>
/// Universal Memory Entity DTO exposed via Host REST API (ADR-004, US-06.1..06.7).
/// </summary>
public sealed record MemoryEntityDto(
    string Id,
    MemoryEntityType EntityType,
    string CanonicalName,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> Titles,
    IReadOnlyList<string> RecognitionVariants,
    string SummaryDescription,
    string? DmPrivateNotes,
    VisibilityScope Visibility,
    IReadOnlyList<string> AuthorizedCharacterIds,
    KnowledgeState KnowledgeState,
    ConfidenceLevel Confidence,
    double InternalConfidenceScore,
    FirstAppearanceRef? FirstAppearance,
    IReadOnlyList<MemoryHistoryItemDto> StateAndBeliefHistory,
    IReadOnlyList<ProvenanceRef> Sources,
    ProcessingLineage? Lineage,
    CharacterFacetDto? CharacterFacet = null);

/// <summary>
/// Embedded state/belief history item DTO (ADR-004, ADR-006, US-06.1).
/// </summary>
public sealed record MemoryHistoryItemDto(
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
/// Request payload to create a new Memory Entity manually or from review (US-06.7, FR-MEM-DM-001).
/// </summary>
public sealed record CreateMemoryEntityRequest(
    MemoryEntityType EntityType,
    string CanonicalName,
    IReadOnlyList<string>? Aliases = null,
    IReadOnlyList<string>? Titles = null,
    IReadOnlyList<string>? RecognitionVariants = null,
    string SummaryDescription = "",
    string? DmPrivateNotes = null,
    VisibilityScope Visibility = VisibilityScope.PartyKnown,
    IReadOnlyList<string>? AuthorizedCharacterIds = null,
    KnowledgeState KnowledgeState = KnowledgeState.CampaignTruth,
    ConfidenceLevel Confidence = ConfidenceLevel.DMConfirmed,
    CharacterFacetDto? CharacterFacet = null,
    FirstAppearanceRef? FirstAppearance = null,
    string CreatedBy = "DM");

/// <summary>
/// Request payload to update an existing Memory Entity (US-06.7, FR-MEM-DM-002).
/// </summary>
public sealed record UpdateMemoryEntityRequest(
    string? CanonicalName = null,
    IReadOnlyList<string>? Aliases = null,
    IReadOnlyList<string>? Titles = null,
    IReadOnlyList<string>? RecognitionVariants = null,
    string? SummaryDescription = null,
    string? DmPrivateNotes = null,
    VisibilityScope? Visibility = null,
    IReadOnlyList<string>? AuthorizedCharacterIds = null,
    KnowledgeState? KnowledgeState = null,
    ConfidenceLevel? Confidence = null,
    CharacterFacetDto? CharacterFacet = null,
    string EditedBy = "DM");
