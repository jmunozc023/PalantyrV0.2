namespace Palantyr.Contracts;

/// <summary>
/// Tracks AI/ML engine, model, adapter, and prompt/schema versions (FR-TRN-007, NFR-MAINT-003, ARCH-CONSTRAINT-003).
/// </summary>
public sealed record ProcessingLineage(
    string EngineName,
    string ModelId,
    string ModelRevision,
    string AdapterVersion,
    string PromptVersion,
    string SchemaVersion,
    DateTimeOffset ProcessedAtUtc);

/// <summary>
/// Traceability pointer back to transcript or DM source (NFR-TRACE-001, FR-MEM-DM-007).
/// </summary>
public sealed record ProvenanceRef(
    ProvenanceSourceType SourceType,
    string? SessionId,
    string? SegmentId,
    string? SpanId,
    long? StartTimestampMs,
    long? EndTimestampMs,
    DateTimeOffset RecordedAtUtc,
    string? NoteOrContext = null);

/// <summary>
/// First appearance metadata for any entity (Section 8, FR-MEM-OBJ-002, FR-MEM-ORG-002).
/// </summary>
public sealed record FirstAppearanceRef(
    DateTimeOffset RealDateUtc,
    string? CampaignDateText,
    string? RelativeTimeContext,
    string? SessionId,
    string? LocationEntityId,
    long? TimestampMs);

/// <summary>
/// Strongly-typed capability token for Pre-Generation Visibility Filtering (ADR-006, Kaede Finding 3).
/// </summary>
public sealed record QueryVisibilityContext(
    bool IncludeDmPrivate,
    IReadOnlyList<string>? ScopedCharacterIds = null)
{
    public static QueryVisibilityContext PartySafe { get; } = new(IncludeDmPrivate: false);
    public static QueryVisibilityContext DmFull { get; } = new(IncludeDmPrivate: true);

    public bool Permits(VisibilityScope scope, IReadOnlyList<string>? authorizedCharacters = null)
    {
        if (scope == VisibilityScope.Ambiguous)
        {
            return IncludeDmPrivate; // Conservative visibility: ambiguous excluded from PartySafe (FR-SUM-005)
        }

        if (IncludeDmPrivate)
        {
            return true;
        }

        if (scope is VisibilityScope.GeneralWorld or VisibilityScope.PartyKnown)
        {
            return true;
        }

        if (scope == VisibilityScope.CharacterScoped && ScopedCharacterIds is { Count: > 0 } && authorizedCharacters is { Count: > 0 })
        {
            return ScopedCharacterIds.Any(id => authorizedCharacters.Contains(id, StringComparer.OrdinalIgnoreCase));
        }

        return false;
    }
}
