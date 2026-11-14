namespace Palantyr.Contracts;

/// <summary>
/// Hardware and audio onboarding assessment service (EPIC-01, FR-SYS-001..005).
/// </summary>
public interface ISystemAssessmentService
{
    Task<SystemAssessmentProfile> RunAssessmentAsync(bool includeBenchmarkProbe = true, CancellationToken cancellationToken = default);
    Task<MicrophoneValidationResult> ValidateMicrophoneAsync(string? deviceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Progressive recoverable audio engine and crash-recovery scanner (EPIC-03, ADR-003).
/// </summary>
public interface IAudioRecordingEngine
{
    Task<CampaignHomeStatusDto> StartSessionRecordingAsync(string campaignId, string sessionId, string? microphoneDeviceId, CancellationToken cancellationToken = default);
    Task<CampaignHomeStatusDto> PauseSessionRecordingAsync(string campaignId, string sessionId, CancellationToken cancellationToken = default);
    Task<CampaignHomeStatusDto> ResumeSessionRecordingAsync(string campaignId, string sessionId, CancellationToken cancellationToken = default);
    Task<EndSessionSummaryDto> EndSessionRecordingAsync(string campaignId, string sessionId, CancellationToken cancellationToken = default);
    Task<CampaignHomeStatusDto> GetLiveStatusAsync(string campaignId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CrashRecoveryReportDto>> ScanAndRecoverInterruptedSessionsAsync(string campaignId, CancellationToken cancellationToken = default);
    Task<CampaignHomeStatusDto> ResolveRecoveredSessionAsync(string campaignId, string sessionId, CrashRecoveryChoice choice, CancellationToken cancellationToken = default);
    Task<byte[]> ExtractAudioContextClipAsync(string campaignId, string sessionId, string segmentId, long centerTimestampMs, int windowSeconds = 10, CancellationToken cancellationToken = default);
}

/// <summary>
/// Runtime priority scheduler & adaptive resource monitor (EPIC-01, FR-PERF-001, ADR-010, Kaede Finding 2).
/// </summary>
public interface IRuntimePriorityScheduler
{
    bool IsBackgroundThrottled { get; }
    string? ThrottlingReasonEn { get; }
    string? ThrottlingReasonEs { get; }
    void NotifySessionStateChanged(SessionRecordingState newState);
    Task<IDisposable> AcquireGpuWorkerSlotAsync(RuntimePriorityTier priority, CancellationToken cancellationToken = default);
    bool ShouldDeferBackgroundWork(double currentCpuPercent, double availableRamGb, double freeStorageGb);
}

/// <summary>
/// Replaceable transcription engine contract (FR-TRN-006, ARCH-CONSTRAINT-001).
/// </summary>
public interface ITranscriptionEngine
{
    string EngineName { get; }
    string ModelId { get; }
    Task<TranscriptSegmentResultDto> TranscribeSegmentAsync(
        string audioFilePath,
        PalantyrLanguage language,
        CancellationToken cancellationToken = default);
}

public sealed record TranscriptUtteranceDto(
    string UtteranceId,
    long StartMs,
    long EndMs,
    string Text,
    double ConfidenceScore);

public sealed record TranscriptSegmentResultDto(
    string SegmentId,
    PalantyrLanguage Language,
    IReadOnlyList<TranscriptUtteranceDto> Utterances,
    ProcessingLineage Lineage);
