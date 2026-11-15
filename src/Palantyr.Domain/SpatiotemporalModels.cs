using Palantyr.Contracts;

namespace Palantyr.Domain;

/// <summary>
/// Transit route descriptor isolating road/transit events from endpoint containment trees (FR-MEM-LOC-003).
/// </summary>
public sealed class TransitLocationRef
{
    public required string OriginLocationId { get; init; }
    public required string DestinationLocationId { get; init; }
    public string? RouteName { get; set; }
    public double? TransitDurationDays { get; set; }

    public TransitLocationRefDto ToDto() => new(
        OriginLocationId: OriginLocationId,
        DestinationLocationId: DestinationLocationId,
        RouteName: RouteName,
        TransitDurationDays: TransitDurationDays);
}

/// <summary>
/// Domain facet for spatial entities with parent/child containment and transit support (Sec 10, US-06.3).
/// </summary>
public sealed class LocationFacet
{
    public LocationType LocationType { get; set; } = LocationType.Settlement;
    public string? ParentLocationId { get; set; }
    public List<string> EnclosedChildIds { get; init; } = [];
    public bool IsTransitPoint { get; set; }
    public TransitLocationRef? TransitEndpoints { get; set; }

    public LocationFacetDto ToDto() => new(
        LocationType: LocationType,
        ParentLocationId: ParentLocationId,
        EnclosedChildIds: EnclosedChildIds.ToList(),
        IsTransitPoint: IsTransitPoint,
        TransitEndpoints: TransitEndpoints?.ToDto());

    public LocationFacetDto? ToPartySafeDto(QueryVisibilityContext context)
    {
        return ToDto();
    }

    /// <summary>
    /// Computes breadcrumb hierarchy (e.g. "Faerun > Sword Coast > Waterdeep > Yawning Portal > Taproom") (UX-MEM-005).
    /// </summary>
    public static string ComputeBreadcrumb(
        string currentLocationId,
        string currentLocationName,
        Func<string, (string Name, string? ParentId)?> resolver,
        int maxDepth = 10)
    {
        var parts = new List<string> { currentLocationName };
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { currentLocationId };
        var currentParentId = resolver(currentLocationId)?.ParentId;

        int depth = 0;
        while (!string.IsNullOrWhiteSpace(currentParentId) && depth < maxDepth)
        {
            if (!visited.Add(currentParentId))
            {
                break; // Cycle protection
            }

            var parentInfo = resolver(currentParentId);
            if (parentInfo is null)
            {
                break;
            }

            parts.Insert(0, parentInfo.Value.Name);
            currentParentId = parentInfo.Value.ParentId;
            depth++;
        }

        return string.Join(" > ", parts);
    }

    /// <summary>
    /// Checks if a location is a child/descendant of the specified ancestor.
    /// </summary>
    public static bool IsDescendantOf(
        string childLocationId,
        string ancestorLocationId,
        Func<string, string?> parentResolver,
        int maxDepth = 15)
    {
        if (string.Equals(childLocationId, ancestorLocationId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { childLocationId };
        var current = parentResolver(childLocationId);
        int depth = 0;

        while (!string.IsNullOrWhiteSpace(current) && depth < maxDepth)
        {
            if (string.Equals(current, ancestorLocationId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!visited.Add(current))
            {
                return false; // Cycle
            }

            current = parentResolver(current);
            depth++;
        }

        return false;
    }
}

/// <summary>
/// Relative day transition boundary detected in speech during play (US-06.4, FR-MEM-TIME-002).
/// </summary>
public sealed class RelativeDayTransition
{
    public required string TransitionId { get; init; }
    public required string SessionId { get; init; }
    public string? SegmentId { get; set; }
    public int FromRelativeDay { get; set; }
    public int ToRelativeDay { get; set; }
    public required string NarrativeMarkerText { get; set; }
    public DateTimeOffset DetectedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public RelativeDayTransitionDto ToDto() => new(
        TransitionId: TransitionId,
        SessionId: SessionId,
        SegmentId: SegmentId,
        FromRelativeDay: FromRelativeDay,
        ToRelativeDay: ToRelativeDay,
        NarrativeMarkerText: NarrativeMarkerText,
        DetectedAtUtc: DetectedAtUtc);
}

/// <summary>
/// Key narrative moment or sub-event along a journey or stay (US-06.4, FR-MEM-EVT-002).
/// </summary>
public sealed class KeyMomentRef
{
    public required string MomentId { get; init; }
    public required string SessionId { get; init; }
    public long? TimestampMs { get; set; }
    public required string SummaryText { get; set; }
    public int? RelativeDayNumber { get; set; }
    public List<string> Participants { get; init; } = [];
    public VisibilityScope Visibility { get; set; } = VisibilityScope.PartyKnown;

    public KeyMomentRefDto ToDto() => new(
        MomentId: MomentId,
        SessionId: SessionId,
        TimestampMs: TimestampMs,
        SummaryText: SummaryText,
        RelativeDayNumber: RelativeDayNumber,
        Participants: Participants.ToList());
}

/// <summary>
/// Domain facet for journeys, multi-day expeditions, and relative temporal continuity (Sec 11, US-06.4, AC-TIME-001).
/// </summary>
public sealed class JourneyFacet
{
    public string? ParentJourneyId { get; set; }
    public string? OriginLocationId { get; set; }
    public string? DestinationLocationId { get; set; }
    public int? TotalEstimatedDays { get; set; }
    public int? CurrentRelativeDay { get; set; }
    public string? RelativeTimeMarker { get; set; }
    public string? DepartureCampaignDate { get; set; }
    public string? ArrivalCampaignDate { get; set; }
    public List<RelativeDayTransition> DayTransitions { get; init; } = [];
    public List<KeyMomentRef> KeyMoments { get; init; } = [];

    public JourneyFacetDto ToDto() => new(
        ParentJourneyId: ParentJourneyId,
        OriginLocationId: OriginLocationId,
        DestinationLocationId: DestinationLocationId,
        TotalEstimatedDays: TotalEstimatedDays,
        CurrentRelativeDay: CurrentRelativeDay,
        RelativeTimeMarker: RelativeTimeMarker,
        DepartureCampaignDate: DepartureCampaignDate,
        ArrivalCampaignDate: ArrivalCampaignDate,
        DayTransitions: DayTransitions.Select(t => t.ToDto()).ToList(),
        KeyMoments: KeyMoments.Select(m => m.ToDto()).ToList());

    public JourneyFacetDto? ToPartySafeDto(QueryVisibilityContext context)
    {
        var safeMoments = KeyMoments
            .Where(m => context.Permits(m.Visibility))
            .Select(m => m.ToDto())
            .ToList();

        return new JourneyFacetDto(
            ParentJourneyId: ParentJourneyId,
            OriginLocationId: OriginLocationId,
            DestinationLocationId: DestinationLocationId,
            TotalEstimatedDays: TotalEstimatedDays,
            CurrentRelativeDay: CurrentRelativeDay,
            RelativeTimeMarker: RelativeTimeMarker,
            DepartureCampaignDate: DepartureCampaignDate,
            ArrivalCampaignDate: ArrivalCampaignDate,
            DayTransitions: DayTransitions.Select(t => t.ToDto()).ToList(),
            KeyMoments: safeMoments);
    }
}
