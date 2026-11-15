namespace Palantyr.Contracts;

/// <summary>
/// Architectural location categorization supporting containment hierarchies (Sec 10, US-06.3).
/// </summary>
public enum LocationType
{
    World = 0,
    Continent = 1,
    Region = 2,
    Settlement = 3,
    District = 4,
    Structure = 5,
    Room = 6,
    PointOfInterest = 7,
    WildernessRoute = 8
}

/// <summary>
/// Transit event route reference preventing false endpoint containment pollution (FR-MEM-LOC-003).
/// </summary>
public sealed record TransitLocationRefDto(
    string OriginLocationId,
    string DestinationLocationId,
    string? RouteName = null,
    double? TransitDurationDays = null);

/// <summary>
/// Domain facet for physical, political, or geographical places (Sec 10, US-06.3, FR-MEM-LOC-001..003).
/// </summary>
public sealed record LocationFacetDto(
    LocationType LocationType,
    string? ParentLocationId,
    IReadOnlyList<string> EnclosedChildIds,
    bool IsTransitPoint,
    TransitLocationRefDto? TransitEndpoints = null);

/// <summary>
/// Relative day transition detected from narrative speech cues (US-06.4, FR-MEM-TIME-002..004).
/// </summary>
public sealed record RelativeDayTransitionDto(
    string TransitionId,
    string SessionId,
    string? SegmentId,
    int FromRelativeDay,
    int ToRelativeDay,
    string NarrativeMarkerText,
    DateTimeOffset DetectedAtUtc);

/// <summary>
/// Key narrative moment or nested sub-event along a journey or stay (US-06.4, FR-MEM-EVT-002).
/// </summary>
public sealed record KeyMomentRefDto(
    string MomentId,
    string SessionId,
    long? TimestampMs,
    string SummaryText,
    int? RelativeDayNumber,
    IReadOnlyList<string> Participants);

/// <summary>
/// Domain facet for multi-day travels, stays, and nested relative-time events (Sec 11, US-06.4, AC-TIME-001).
/// </summary>
public sealed record JourneyFacetDto(
    string? ParentJourneyId,
    string? OriginLocationId,
    string? DestinationLocationId,
    int? TotalEstimatedDays,
    int? CurrentRelativeDay,
    string? RelativeTimeMarker,
    string? DepartureCampaignDate,
    string? ArrivalCampaignDate,
    IReadOnlyList<RelativeDayTransitionDto> DayTransitions,
    IReadOnlyList<KeyMomentRefDto> KeyMoments);

/// <summary>
/// Tree node for hierarchy visualization in the UI (e.g. World > Region > City > Tavern > Cellar) (UX-MEM-005).
/// </summary>
public sealed record LocationHierarchyNodeDto(
    string LocationId,
    string CanonicalName,
    LocationType LocationType,
    string? ParentLocationId,
    string BreadcrumbPath,
    IReadOnlyList<LocationHierarchyNodeDto> Children);
