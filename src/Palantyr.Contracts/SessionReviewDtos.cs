using System;
using System.Collections.Generic;

namespace Palantyr.Contracts;

/// <summary>
/// DM resolution action for a Needs Attention item (US-09.1, US-09.2, FR-REV-010..012).
/// </summary>
public enum NeedsAttentionAction
{
    Confirm = 0,
    RejectWithTombstone = 1,
    DeferOrUnresolved = 2,
    LinkToExisting = 3,
    ClassifyAsJoke = 4,
    PromoteToCanon = 5
}

/// <summary>
/// Resolution request payload for a Needs Attention item (US-09.2, AC-REV-001).
/// </summary>
public sealed record ResolveAttentionItemRequest(
    NeedsAttentionAction Action,
    string? TargetEntityId = null,
    string? CorrectionText = null,
    string? Notes = null,
    string ResolvedBy = "DM");

/// <summary>
/// Compact memory item presented during Full Review (US-09.4, AC-REV-002).
/// </summary>
public sealed record MemoryReviewItemDto(
    string EntityId,
    string CanonicalName,
    MemoryEntityType EntityType,
    string ClaimOrStatement,
    KnowledgeState KnowledgeState,
    ConfidenceLevel Confidence,
    string? SourceSessionId,
    long? TimestampMs,
    bool IsConfirmed = false,
    bool IsRejected = false);

/// <summary>
/// Individual decision within a batch review (US-09.4, FR-REV-014..016).
/// </summary>
public sealed record SingleReviewDecisionDto(
    string EntityId,
    bool IsConfirmed,
    bool IsRejected,
    string? CorrectionText = null,
    string? RejectionReason = null);

/// <summary>
/// Batch review payload with immediate autosave and Undo support (US-09.4, US-09.5, AC-REV-002).
/// </summary>
public sealed record BatchReviewRequest(
    string SessionId,
    IReadOnlyList<SingleReviewDecisionDto> Decisions,
    string DecidedBy = "DM");

/// <summary>
/// Snapshot token enabling immediate Undo of a batch review action (US-09.5, NFR-USAB-008).
/// </summary>
public sealed record ReviewUndoToken(
    string UndoTokenId,
    string CampaignId,
    string SessionId,
    DateTimeOffset ExecutedAtUtc,
    IReadOnlyList<string> AffectedEntityIds);

/// <summary>
/// 10-second audio context playback request (US-09.3, FR-VAL-002).
/// </summary>
public sealed record AudioClipRequest(
    string SessionId,
    string SegmentId,
    long TargetTimestampMs,
    double PreRollSeconds = 5.0,
    double PostRollSeconds = 5.0);

/// <summary>
/// Audio clip result for review playback (US-09.3, FR-VAL-002).
/// </summary>
public sealed record AudioClipResult(
    string ClipFilePath,
    long StartTimestampMs,
    long EndTimestampMs,
    double DurationSeconds);
