using System;
using System.Collections.Generic;

namespace Palantyr.Contracts;

/// <summary>
/// Visibility mode for session summary generation (ADR-006, US-08.2).
/// </summary>
public enum SummaryVisibilityMode
{
    PartySafe = 0,
    DmFull = 1
}

/// <summary>
/// Key narrative event or causal milestone in a session summary (US-08.1, US-08.2).
/// </summary>
public sealed record SummaryKeyEventDto(
    string EventId,
    string Description,
    string? RelativeTimeContext,
    string? LocationId,
    IReadOnlyList<string> Participants,
    VisibilityScope Visibility);

/// <summary>
/// Significant discovery, revelation, or canon shift in a session (US-08.1).
/// </summary>
public sealed record SummaryDiscoveryDto(
    string Subject,
    string Description,
    KnowledgeState KnowledgeState,
    ConfidenceLevel Confidence);

/// <summary>
/// Context-aware narrative session summary (ADR-006, EPIC-08, US-08.1..US-08.5).
/// Guaranteed zero-leakage of DM private notes when Mode == SummaryVisibilityMode.PartySafe.
/// </summary>
public sealed record SessionSummaryDto(
    string SummaryId,
    string CampaignId,
    string SessionId,
    SummaryVisibilityMode Mode,
    string Title,
    string ExecutiveRecap,
    IReadOnlyList<SummaryKeyEventDto> KeyEvents,
    IReadOnlyList<SummaryDiscoveryDto> MajorDiscoveries,
    IReadOnlyList<string> OutstandingQuestions,
    string? DmPrivateNotes,
    DateTimeOffset GeneratedAtUtc,
    string PromptTemplateVersion,
    IReadOnlyList<ProvenanceRef> Sources);

/// <summary>
/// Request payload to generate a session summary (US-08.2, FR-SUM-001..005).
/// </summary>
public sealed record GenerateSummaryRequest(
    SummaryVisibilityMode Mode,
    PalantyrLanguage TargetLanguage = PalantyrLanguage.English,
    bool IncludeDmPrivateNotes = false,
    string? CustomFocusNotes = null);

/// <summary>
/// Request payload to update or correct an existing session summary (US-08.4, AC-COR-001).
/// </summary>
public sealed record UpdateSummaryRequest(
    string? Title = null,
    string? ExecutiveRecap = null,
    IReadOnlyList<SummaryKeyEventDto>? KeyEvents = null,
    IReadOnlyList<SummaryDiscoveryDto>? MajorDiscoveries = null,
    IReadOnlyList<string>? OutstandingQuestions = null,
    string? DmPrivateNotes = null,
    string? DmCorrectionNote = null,
    string CorrectedBy = "DM");
