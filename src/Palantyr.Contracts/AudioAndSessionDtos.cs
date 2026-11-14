namespace Palantyr.Contracts;

/// <summary>
/// Entry inside the authoritative filesystem Write-Ahead Log `session_recording.pseg` (ADR-003, Kaede Finding 1).
/// </summary>
public sealed record AudioSegmentWalEntry(
    string SessionId,
    string SegmentId,
    int SequenceNumber,
    string FilePath,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset LastFlushedAtUtc,
    long FlushedPcmBytes,
    long DurationMs,
    AudioSegmentWalStatus Status,
    string? ClosureReason = null);

/// <summary>
/// Explicit subsystem status indicators for Campaign Home (UX-HOME-003..007, AC-USAB-001).
/// </summary>
public sealed record CampaignHomeStatusDto(
    string CampaignId,
    string? ActiveSessionId,
    SessionRecordingState SessionState,
    EonixState EonixVisualState,
    SubsystemProcessingState RecordingSaveStatus,
    SubsystemProcessingState TranscriptionStatus,
    SubsystemProcessingState MemoryProcessingStatus,
    SubsystemProcessingState EonixQueryStatus,
    long TotalRecordedDurationMs,
    int SafelyStoredSegmentsCount,
    int TranscribedSegmentsCount,
    int MemoryProcessedSegmentsCount,
    int NeedsAttentionCount,
    double FreeStorageGb,
    bool IsStorageWarningActive,
    bool IsBackgroundThrottled,
    string? ThrottlingReasonEn,
    string? ThrottlingReasonEs);

/// <summary>
/// Post-session status presented after Two-Stage End Session (UX-END-002..003, FR-END-001..004).
/// </summary>
public sealed record EndSessionSummaryDto(
    string CampaignId,
    string SessionId,
    long TotalRecordedDurationMs,
    int SafelyStoredSegments,
    int TranscribedSegments,
    int MemoryProcessedSegments,
    int NeedsAttentionItemsCount,
    bool ProcessingRemaining);

/// <summary>
/// Crash / interruption recovery report (FR-REC-006..008, AC-REC-003).
/// </summary>
public sealed record CrashRecoveryReportDto(
    string CampaignId,
    string SessionId,
    DateTimeOffset InterruptedAtUtc,
    int PreviouslySafeSegmentsCount,
    int RecoveredSegmentsCount,
    int UnrecoverableSegmentsCount,
    long TotalRecoveredDurationMs,
    IReadOnlyList<AudioSegmentWalEntry> Segments,
    string SummaryMessageEn,
    string SummaryMessageEs);

/// <summary>
/// DM recovery decision for an interrupted session (FR-REC-008).
/// </summary>
public enum CrashRecoveryChoice
{
    ContinueSession = 0,
    EndSessionAndProcess = 1,
    CloseSafelyAndStartNew = 2
}

/// <summary>
/// Safe campaign deletion options (Section 38 Safe Campaign Deletion, AC-DATA-006).
/// </summary>
public enum CampaignDeletionOption
{
    CreateBackupAndDelete = 0,
    DeleteWithoutBackup = 1,
    Cancel = 2
}
