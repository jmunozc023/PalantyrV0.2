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
        IReadOnlyList<string>? fantasyGlossary = null,
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

/// <summary>
/// Request to transcribe a closed audio segment (US-04.3, FR-TRN-002).
/// </summary>
public sealed record TranscriptionJobRequest(
    string CampaignId,
    string SessionId,
    string SegmentId,
    string AudioFilePath,
    PalantyrLanguage Language,
    int Priority = 0,
    IReadOnlyList<string>? FantasyGlossary = null);

/// <summary>
/// Result of processing a transcription job.
/// </summary>
public sealed record TranscriptionJobResult(
    string CampaignId,
    string SessionId,
    string SegmentId,
    bool Success,
    TranscriptSegmentResultDto? Transcript,
    string? ErrorMessage);

/// <summary>
/// Status of an enqueued transcription job in the persistent processing queue (UX-HOME-005).
/// </summary>
public sealed record TranscriptionJobStatusDto(
    string JobId,
    string CampaignId,
    string SessionId,
    string SegmentId,
    SubsystemProcessingState State,
    DateTimeOffset EnqueuedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    int RetryCount,
    string? ErrorMessage);

/// <summary>
/// Persistent transcription queue interface with priority and throttling awareness (US-04.3, US-04.5).
/// </summary>
public interface ITranscriptionQueue
{
    int PendingCount { get; }
    int ProcessingCount { get; }
    void EnqueueSegment(TranscriptionJobRequest request);
    Task<TranscriptionJobResult?> ProcessNextJobAsync(CancellationToken cancellationToken = default);
    IReadOnlyList<TranscriptionJobStatusDto> GetQueueStatus(string campaignId, string? sessionId = null);
}

/// <summary>
/// Working transcript span DTO with context classification and correction layers (ADR-005, US-04.4, US-05.1).
/// </summary>
public sealed record WorkingTranscriptSpanDto(
    string SpanId,
    string SegmentId,
    long StartMs,
    long EndMs,
    string CorrectedText,
    string OriginalText,
    ConversationContextType ContextType,
    double ContextConfidence,
    bool IsManuallyConfirmed,
    string? PromotionReason = null,
    string? ConfirmedBy = null,
    ProcessingLineage? Lineage = null);

/// <summary>
/// Conversation context classifier contract (EPIC-05, ADR-005, FR-CTX-001..010).
/// </summary>
public interface IContextClassifier
{
    string ClassifierName { get; }
    string ModelOrRuleVersion { get; }

    Task<IReadOnlyList<WorkingTranscriptSpanDto>> ClassifySpansAsync(
        string campaignId,
        string sessionId,
        string segmentId,
        IReadOnlyList<TranscriptUtteranceDto> utterances,
        IReadOnlyList<string>? partyCharacterNames = null,
        CancellationToken cancellationToken = default);

    WorkingTranscriptSpanDto PromoteJokeToCanon(
        WorkingTranscriptSpanDto span,
        string confirmationReason,
        string confirmedBy = "DM");
}
