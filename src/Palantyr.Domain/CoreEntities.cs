using Palantyr.Contracts;

namespace Palantyr.Domain;

/// <summary>
/// Character statement reference tracking statements made BY or ABOUT a character (Sec 9, US-06.2).
/// </summary>
public sealed class CharacterStatement
{
    public required string StatementId { get; init; }
    public required string SessionId { get; init; }
    public string? SegmentId { get; init; }
    public string? SpanId { get; init; }
    public long? TimestampMs { get; init; }
    public required string SummaryText { get; set; }
    public string? TargetSubject { get; set; }
    public bool IsByCharacter { get; init; }
    public VisibilityScope Visibility { get; set; } = VisibilityScope.PartyKnown;
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public CharacterStatementDto ToDto() => new(
        StatementId: StatementId,
        SessionId: SessionId,
        SegmentId: SegmentId,
        SpanId: SpanId,
        TimestampMs: TimestampMs,
        SummaryText: SummaryText,
        TargetSubject: TargetSubject,
        IsByCharacter: IsByCharacter,
        Visibility: Visibility);
}

/// <summary>
/// Domain facet for characters (NPCs, PCs, and historical figures) (Sec 9, US-06.2, FR-MEM-CHAR-001..002).
/// </summary>
public sealed class CharacterFacet
{
    public string? RaceOrSpecies { get; set; }
    public string? PhysicalDescription { get; set; }
    public string? RoleOrArchetype { get; set; }
    public List<string> Affiliations { get; init; } = [];
    public List<CharacterStatement> StatementsBy { get; init; } = [];
    public List<CharacterStatement> StatementsAbout { get; init; } = [];

    public CharacterFacetDto ToDto() => new(
        RaceOrSpecies: RaceOrSpecies,
        PhysicalDescription: PhysicalDescription,
        RoleOrArchetype: RoleOrArchetype,
        Affiliations: Affiliations.ToList(),
        StatementsBy: StatementsBy.Select(s => s.ToDto()).ToList(),
        StatementsAbout: StatementsAbout.Select(s => s.ToDto()).ToList());

    /// <summary>
    /// Projects a Party-Safe character facet, filtering out any statements marked DMPrivate (ADR-006, Kaede Finding 3).
    /// </summary>
    public CharacterFacetDto? ToPartySafeDto(QueryVisibilityContext context)
    {
        var safeBy = StatementsBy
            .Where(s => context.Permits(s.Visibility))
            .Select(s => s.ToDto())
            .ToList();

        var safeAbout = StatementsAbout
            .Where(s => context.Permits(s.Visibility))
            .Select(s => s.ToDto())
            .ToList();

        return new CharacterFacetDto(
            RaceOrSpecies: RaceOrSpecies,
            PhysicalDescription: PhysicalDescription,
            RoleOrArchetype: RoleOrArchetype,
            Affiliations: Affiliations.ToList(),
            StatementsBy: safeBy,
            StatementsAbout: safeAbout);
    }
}
