using System.Collections.Concurrent;
using Palantyr.Contracts;
using Palantyr.Domain;

namespace Palantyr.Audio;

/// <summary>
/// Progressive recoverable audio engine and deterministic crash-recovery scanner
/// implementing <see cref="IAudioRecordingEngine"/> (EPIC-03, ADR-003, Kaede Finding 1).
/// </summary>
public sealed class AudioRecordingEngine : IAudioRecordingEngine, IDisposable
{
    private readonly ICampaignMemoryStore _campaignMemoryStore;
    private readonly ISystemStore? _systemStore;
    private readonly IRuntimePriorityScheduler? _priorityScheduler;
    private readonly PsegWalManager _walManager;
    private readonly StorageSpaceWatchdog _storageWatchdog;
    private readonly SemaphoreSlim _engineLock = new(1, 1);
    private readonly ConcurrentDictionary<string, ActiveSessionRuntimeState> _activeSessions = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public AudioRecordingEngine(
        ICampaignMemoryStore campaignMemoryStore,
        ISystemStore? systemStore = null,
        IRuntimePriorityScheduler? priorityScheduler = null,
        PsegWalManager? walManager = null,
        StorageSpaceWatchdog? storageWatchdog = null)
    {
        ArgumentNullException.ThrowIfNull(campaignMemoryStore);
        _campaignMemoryStore = campaignMemoryStore;
        _systemStore = systemStore;
        _priorityScheduler = priorityScheduler;
        _walManager = walManager ?? new PsegWalManager();
        _storageWatchdog = storageWatchdog ?? new StorageSpaceWatchdog(_walManager.BaseStorageDirectory);
    }

    public PsegWalManager WalManager => _walManager;
    public StorageSpaceWatchdog StorageWatchdog => _storageWatchdog;

    /// <inheritdoc />
    public async Task<CampaignHomeStatusDto> StartSessionRecordingAsync(
        string campaignId,
        string sessionId,
        string? microphoneDeviceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        await _engineLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var storageStatus = _storageWatchdog.Evaluate(_walManager.GetCampaignDirectory(campaignId));
            var existingSessions = await _campaignMemoryStore.ListSessionsAsync(campaignId, cancellationToken).ConfigureAwait(false);
            int sessionNumber = existingSessions.Count + 1;

            var existingSession = await _campaignMemoryStore.GetSessionAsync(campaignId, sessionId, cancellationToken).ConfigureAwait(false);
            var nowUtc = DateTimeOffset.UtcNow;

            if (storageStatus.IsCriticalStorageExhausted)
            {
                var pausedDoc = existingSession ?? new SessionDocument
                {
                    Id = sessionId,
                    CampaignId = campaignId,
                    SessionNumber = sessionNumber,
                    Title = $"Session {sessionNumber}",
                    RecordingState = SessionRecordingState.Paused,
                    TranscriptionState = SubsystemProcessingState.Pending,
                    MemoryProcessingState = SubsystemProcessingState.Pending,
                    StartedAtUtc = nowUtc
                };
                pausedDoc.RecordingState = SessionRecordingState.Paused;

                await _campaignMemoryStore.UpsertSessionFromWalAsync(campaignId, pausedDoc, cancellationToken).ConfigureAwait(false);
                _priorityScheduler?.NotifySessionStateChanged(SessionRecordingState.Paused);

                return await BuildCampaignHomeStatusUnlockedAsync(campaignId, pausedDoc, storageStatus, cancellationToken).ConfigureAwait(false);
            }

            var existingWalEntries = await _walManager.ReadEntriesAsync(campaignId, sessionId, cancellationToken).ConfigureAwait(false);
            int nextSeq = existingWalEntries.Count == 0
                ? 1
                : existingWalEntries.Max(e => e.SequenceNumber) + 1;

            var activeEntry = await OpenNewActiveSegmentUnlockedAsync(
                campaignId,
                sessionId,
                nextSeq,
                initialPcmBytes: WavRiffHeaderHelper.TwoSecondChunkBytes,
                cancellationToken).ConfigureAwait(false);

            var allSegments = await _walManager.ReadEntriesAsync(campaignId, sessionId, cancellationToken).ConfigureAwait(false);

            var sessionDoc = existingSession ?? new SessionDocument
            {
                Id = sessionId,
                CampaignId = campaignId,
                SessionNumber = sessionNumber,
                Title = $"Session {sessionNumber}",
                StartedAtUtc = nowUtc
            };

            sessionDoc.RecordingState = SessionRecordingState.Recording;
            sessionDoc.TranscriptionState = SubsystemProcessingState.Pending;
            sessionDoc.MemoryProcessingState = SubsystemProcessingState.Pending;
            sessionDoc.Segments = allSegments.ToList();
            sessionDoc.TotalRecordedDurationMs = allSegments.Sum(s => s.DurationMs);

            _activeSessions[GetSessionKey(campaignId, sessionId)] = new ActiveSessionRuntimeState(
                CampaignId: campaignId,
                SessionId: sessionId,
                MicrophoneDeviceId: microphoneDeviceId,
                ActiveSegment: activeEntry);

            await _campaignMemoryStore.UpsertSessionFromWalAsync(campaignId, sessionDoc, cancellationToken).ConfigureAwait(false);
            _priorityScheduler?.NotifySessionStateChanged(SessionRecordingState.Recording);

            return await BuildCampaignHomeStatusUnlockedAsync(campaignId, sessionDoc, storageStatus, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _engineLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<CampaignHomeStatusDto> PauseSessionRecordingAsync(
        string campaignId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        await _engineLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await CloseActiveSegmentsSafelyUnlockedAsync(
                campaignId,
                sessionId,
                closureReason: "UserPaused",
                cancellationToken).ConfigureAwait(false);

            var allSegments = await _walManager.ReadEntriesAsync(campaignId, sessionId, cancellationToken).ConfigureAwait(false);
            var sessionDoc = await GetOrCreateSessionDocumentUnlockedAsync(campaignId, sessionId, cancellationToken).ConfigureAwait(false);

            sessionDoc.RecordingState = SessionRecordingState.Paused;
            sessionDoc.Segments = allSegments.ToList();
            sessionDoc.TotalRecordedDurationMs = allSegments.Sum(s => s.DurationMs);

            _activeSessions.TryRemove(GetSessionKey(campaignId, sessionId), out _);

            await _campaignMemoryStore.UpsertSessionFromWalAsync(campaignId, sessionDoc, cancellationToken).ConfigureAwait(false);
            _priorityScheduler?.NotifySessionStateChanged(SessionRecordingState.Paused);

            var storageStatus = _storageWatchdog.Evaluate(_walManager.GetCampaignDirectory(campaignId));
            return await BuildCampaignHomeStatusUnlockedAsync(campaignId, sessionDoc, storageStatus, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _engineLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<CampaignHomeStatusDto> ResumeSessionRecordingAsync(
        string campaignId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        await _engineLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var storageStatus = _storageWatchdog.Evaluate(_walManager.GetCampaignDirectory(campaignId));
            var sessionDoc = await GetOrCreateSessionDocumentUnlockedAsync(campaignId, sessionId, cancellationToken).ConfigureAwait(false);

            if (storageStatus.IsCriticalStorageExhausted)
            {
                await CloseActiveSegmentsSafelyUnlockedAsync(
                    campaignId,
                    sessionId,
                    closureReason: "CriticalStorageExhausted",
                    cancellationToken).ConfigureAwait(false);

                var segmentsAfterStop = await _walManager.ReadEntriesAsync(campaignId, sessionId, cancellationToken).ConfigureAwait(false);
                sessionDoc.RecordingState = SessionRecordingState.Paused;
                sessionDoc.Segments = segmentsAfterStop.ToList();
                sessionDoc.TotalRecordedDurationMs = segmentsAfterStop.Sum(s => s.DurationMs);

                await _campaignMemoryStore.UpsertSessionFromWalAsync(campaignId, sessionDoc, cancellationToken).ConfigureAwait(false);
                _priorityScheduler?.NotifySessionStateChanged(SessionRecordingState.Paused);

                return await BuildCampaignHomeStatusUnlockedAsync(campaignId, sessionDoc, storageStatus, cancellationToken).ConfigureAwait(false);
            }

            // Ensure any prior active segment is closed before opening the new resumed segment
            await CloseActiveSegmentsSafelyUnlockedAsync(
                campaignId,
                sessionId,
                closureReason: "ClosedBeforeResume",
                cancellationToken).ConfigureAwait(false);

            var existingSegments = await _walManager.ReadEntriesAsync(campaignId, sessionId, cancellationToken).ConfigureAwait(false);
            int nextSeq = existingSegments.Count == 0
                ? 1
                : existingSegments.Max(s => s.SequenceNumber) + 1;

            var activeEntry = await OpenNewActiveSegmentUnlockedAsync(
                campaignId,
                sessionId,
                nextSeq,
                initialPcmBytes: WavRiffHeaderHelper.TwoSecondChunkBytes,
                cancellationToken).ConfigureAwait(false);

            var allSegments = await _walManager.ReadEntriesAsync(campaignId, sessionId, cancellationToken).ConfigureAwait(false);

            sessionDoc.RecordingState = SessionRecordingState.Recording;
            sessionDoc.Segments = allSegments.ToList();
            sessionDoc.TotalRecordedDurationMs = allSegments.Sum(s => s.DurationMs);

            _activeSessions[GetSessionKey(campaignId, sessionId)] = new ActiveSessionRuntimeState(
                CampaignId: campaignId,
                SessionId: sessionId,
                MicrophoneDeviceId: null,
                ActiveSegment: activeEntry);

            await _campaignMemoryStore.UpsertSessionFromWalAsync(campaignId, sessionDoc, cancellationToken).ConfigureAwait(false);
            _priorityScheduler?.NotifySessionStateChanged(SessionRecordingState.Recording);

            return await BuildCampaignHomeStatusUnlockedAsync(campaignId, sessionDoc, storageStatus, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _engineLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<EndSessionSummaryDto> EndSessionRecordingAsync(
        string campaignId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        await _engineLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Stage 1: Immediate safe audio closure (FR-END-001..002)
            await CloseActiveSegmentsSafelyUnlockedAsync(
                campaignId,
                sessionId,
                closureReason: "SessionEnded",
                cancellationToken).ConfigureAwait(false);

            _activeSessions.TryRemove(GetSessionKey(campaignId, sessionId), out _);

            var allSegments = await _walManager.ReadEntriesAsync(campaignId, sessionId, cancellationToken).ConfigureAwait(false);
            var sessionDoc = await GetOrCreateSessionDocumentUnlockedAsync(campaignId, sessionId, cancellationToken).ConfigureAwait(false);

            sessionDoc.RecordingState = SessionRecordingState.Ended;
            sessionDoc.EndedAtUtc = DateTimeOffset.UtcNow;
            sessionDoc.Segments = allSegments.ToList();
            sessionDoc.TotalRecordedDurationMs = allSegments.Sum(s => s.DurationMs);

            await _campaignMemoryStore.UpsertSessionFromWalAsync(campaignId, sessionDoc, cancellationToken).ConfigureAwait(false);
            _priorityScheduler?.NotifySessionStateChanged(SessionRecordingState.Ended);

            int safelyStoredCount = allSegments.Count(s =>
                s.Status is AudioSegmentWalStatus.ClosedSafe
                    or AudioSegmentWalStatus.RecoveredFromCrash
                    or AudioSegmentWalStatus.CleanedUpAfterProcessing);

            int transcribedCount = sessionDoc.TranscriptionState == SubsystemProcessingState.Completed
                ? safelyStoredCount
                : 0;
            int memoryProcessedCount = sessionDoc.MemoryProcessingState == SubsystemProcessingState.Completed
                ? safelyStoredCount
                : 0;

            var entities = await _campaignMemoryStore.QueryDmFullMemoryAsync(campaignId, cancellationToken: cancellationToken).ConfigureAwait(false);
            int needsAttentionCount = entities.Count(e =>
                e.Confidence == ConfidenceLevel.LowOrUncertain ||
                e.KnowledgeState == KnowledgeState.UnknownOrUnresolved ||
                e.Visibility == VisibilityScope.Ambiguous);

            bool processingRemaining = safelyStoredCount > 0 &&
                (sessionDoc.TranscriptionState != SubsystemProcessingState.Completed ||
                 sessionDoc.MemoryProcessingState != SubsystemProcessingState.Completed);

            return new EndSessionSummaryDto(
                CampaignId: campaignId,
                SessionId: sessionId,
                TotalRecordedDurationMs: sessionDoc.TotalRecordedDurationMs,
                SafelyStoredSegments: safelyStoredCount,
                TranscribedSegments: transcribedCount,
                MemoryProcessedSegments: memoryProcessedCount,
                NeedsAttentionItemsCount: needsAttentionCount,
                ProcessingRemaining: processingRemaining);
        }
        finally
        {
            _engineLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<CampaignHomeStatusDto> GetLiveStatusAsync(
        string campaignId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);

        await _engineLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var sessions = await _campaignMemoryStore.ListSessionsAsync(campaignId, cancellationToken).ConfigureAwait(false);
            var latestSession = sessions
                .OrderByDescending(s => s.StartedAtUtc)
                .FirstOrDefault();

            var storageStatus = _storageWatchdog.Evaluate(_walManager.GetCampaignDirectory(campaignId));

            // If we are actively recording and disk falls below critical 250 MB threshold, close segment & pause immediately (NFR-DATA-003)
            if (latestSession is { RecordingState: SessionRecordingState.Recording } && storageStatus.IsCriticalStorageExhausted)
            {
                await CloseActiveSegmentsSafelyUnlockedAsync(
                    campaignId,
                    latestSession.Id,
                    closureReason: "CriticalStorageExhausted",
                    cancellationToken).ConfigureAwait(false);

                _activeSessions.TryRemove(GetSessionKey(campaignId, latestSession.Id), out _);
                var updatedSegments = await _walManager.ReadEntriesAsync(campaignId, latestSession.Id, cancellationToken).ConfigureAwait(false);
                latestSession.RecordingState = SessionRecordingState.Paused;
                latestSession.Segments = updatedSegments.ToList();
                latestSession.TotalRecordedDurationMs = updatedSegments.Sum(s => s.DurationMs);

                await _campaignMemoryStore.UpsertSessionFromWalAsync(campaignId, latestSession, cancellationToken).ConfigureAwait(false);
                _priorityScheduler?.NotifySessionStateChanged(SessionRecordingState.Paused);
            }

            return await BuildCampaignHomeStatusUnlockedAsync(campaignId, latestSession, storageStatus, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _engineLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CrashRecoveryReportDto>> ScanAndRecoverInterruptedSessionsAsync(
        string campaignId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);

        await _engineLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var unclosedSessions = await _walManager.ScanCampaignForUnclosedSessionsAsync(campaignId, cancellationToken).ConfigureAwait(false);
            if (unclosedSessions.Count == 0)
            {
                return [];
            }

            var reports = new List<CrashRecoveryReportDto>(unclosedSessions.Count);

            foreach (var (sessionId, entries) in unclosedSessions)
            {
                int previouslySafeCount = 0;
                int recoveredCount = 0;
                int unrecoverableCount = 0;
                DateTimeOffset interruptedAtUtc = DateTimeOffset.UtcNow;

                foreach (var entry in entries)
                {
                    if (entry.Status is AudioSegmentWalStatus.ClosedSafe or AudioSegmentWalStatus.RecoveredFromCrash or AudioSegmentWalStatus.CleanedUpAfterProcessing)
                    {
                        previouslySafeCount++;
                        continue;
                    }

                    if (entry.Status == AudioSegmentWalStatus.CorruptedUnrecoverable)
                    {
                        unrecoverableCount++;
                        continue;
                    }

                    if (entry.Status == AudioSegmentWalStatus.Active)
                    {
                        interruptedAtUtc = entry.LastFlushedAtUtc;

                        if (File.Exists(entry.FilePath))
                        {
                            var fileInfo = new FileInfo(entry.FilePath);
                            if (fileInfo.Length >= WavRiffHeaderHelper.HeaderSizeBytes)
                            {
                                long recoveredDurationMs = WavRiffHeaderHelper.RepairUnclosedWavHeader(entry.FilePath);
                                long recoveredPcmBytes = Math.Max(0L, fileInfo.Length - WavRiffHeaderHelper.HeaderSizeBytes);

                                var recoveredEntry = entry with
                                {
                                    LastFlushedAtUtc = DateTimeOffset.UtcNow,
                                    FlushedPcmBytes = recoveredPcmBytes,
                                    DurationMs = recoveredDurationMs,
                                    Status = AudioSegmentWalStatus.RecoveredFromCrash,
                                    ClosureReason = "RecoveredFromCrashOnStartup"
                                };

                                await _walManager.AppendOrUpdateEntryAsync(campaignId, sessionId, recoveredEntry, cancellationToken).ConfigureAwait(false);
                                recoveredCount++;
                                continue;
                            }
                        }

                        var corruptedEntry = entry with
                        {
                            LastFlushedAtUtc = DateTimeOffset.UtcNow,
                            FlushedPcmBytes = 0,
                            DurationMs = 0,
                            Status = AudioSegmentWalStatus.CorruptedUnrecoverable,
                            ClosureReason = "MissingOrTruncatedWavOnCrashRecovery"
                        };

                        await _walManager.AppendOrUpdateEntryAsync(campaignId, sessionId, corruptedEntry, cancellationToken).ConfigureAwait(false);
                        unrecoverableCount++;
                    }
                }

                var reconciledSegments = await _walManager.ReadEntriesAsync(campaignId, sessionId, cancellationToken).ConfigureAwait(false);
                long totalRecoveredDurationMs = reconciledSegments
                    .Where(s => s.Status is AudioSegmentWalStatus.ClosedSafe or AudioSegmentWalStatus.RecoveredFromCrash or AudioSegmentWalStatus.CleanedUpAfterProcessing)
                    .Sum(s => s.DurationMs);

                // Deterministically reconcile into RavenDB / ICampaignMemoryStore BEFORE UI loads (Kaede Finding 1)
                var sessionDoc = await GetOrCreateSessionDocumentUnlockedAsync(campaignId, sessionId, cancellationToken).ConfigureAwait(false);
                sessionDoc.RecordingState = SessionRecordingState.Recovering;
                sessionDoc.Segments = reconciledSegments.ToList();
                sessionDoc.TotalRecordedDurationMs = totalRecoveredDurationMs;

                await _campaignMemoryStore.UpsertSessionFromWalAsync(campaignId, sessionDoc, cancellationToken).ConfigureAwait(false);

                string summaryEn = $"Session '{sessionId}' was interrupted at {interruptedAtUtc:u}. Safely preserved: {previouslySafeCount} prior segment(s) and {recoveredCount} repaired segment(s) ({totalRecoveredDurationMs / 1000.0:F1}s total). Unrecoverable segments: {unrecoverableCount}.";
                string summaryEs = $"La sesión '{sessionId}' se interrumpió a las {interruptedAtUtc:u}. Preservados de forma segura: {previouslySafeCount} segmento(s) previo(s) y {recoveredCount} segmento(s) reparado(s) ({totalRecoveredDurationMs / 1000.0:F1}s en total). Segmentos irrecuperables: {unrecoverableCount}.";

                reports.Add(new CrashRecoveryReportDto(
                    CampaignId: campaignId,
                    SessionId: sessionId,
                    InterruptedAtUtc: interruptedAtUtc,
                    PreviouslySafeSegmentsCount: previouslySafeCount,
                    RecoveredSegmentsCount: recoveredCount,
                    UnrecoverableSegmentsCount: unrecoverableCount,
                    TotalRecoveredDurationMs: totalRecoveredDurationMs,
                    Segments: reconciledSegments,
                    SummaryMessageEn: summaryEn,
                    SummaryMessageEs: summaryEs));
            }

            return reports;
        }
        finally
        {
            _engineLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<CampaignHomeStatusDto> ResolveRecoveredSessionAsync(
        string campaignId,
        string sessionId,
        CrashRecoveryChoice choice,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        switch (choice)
        {
            case CrashRecoveryChoice.ContinueSession:
                return await ResumeSessionRecordingAsync(campaignId, sessionId, cancellationToken).ConfigureAwait(false);

            case CrashRecoveryChoice.EndSessionAndProcess:
                await EndSessionRecordingAsync(campaignId, sessionId, cancellationToken).ConfigureAwait(false);
                return await GetLiveStatusAsync(campaignId, cancellationToken).ConfigureAwait(false);

            case CrashRecoveryChoice.CloseSafelyAndStartNew:
                await EndSessionRecordingAsync(campaignId, sessionId, cancellationToken).ConfigureAwait(false);
                string newSessionId = $"{sessionId}-resumed-{Guid.NewGuid():N}"[..Math.Min(36, sessionId.Length + 17)];
                return await StartSessionRecordingAsync(campaignId, newSessionId, microphoneDeviceId: null, cancellationToken).ConfigureAwait(false);

            default:
                throw new ArgumentOutOfRangeException(nameof(choice), choice, "Unsupported crash recovery choice.");
        }
    }

    /// <inheritdoc />
    public async Task<byte[]> ExtractAudioContextClipAsync(
        string campaignId,
        string sessionId,
        string segmentId,
        long centerTimestampMs,
        int windowSeconds = 10,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(segmentId);

        var entries = await _walManager.ReadEntriesAsync(campaignId, sessionId, cancellationToken).ConfigureAwait(false);
        var target = entries.FirstOrDefault(e => string.Equals(e.SegmentId, segmentId, StringComparison.OrdinalIgnoreCase))
                     ?? entries.FirstOrDefault();

        if (target is null || !File.Exists(target.FilePath))
        {
            throw new FileNotFoundException($"Audio segment '{segmentId}' for session '{sessionId}' does not exist on disk.");
        }

        return WavRiffHeaderHelper.ExtractPcmSliceAsWav(target.FilePath, centerTimestampMs, windowSeconds);
    }

    /// <summary>
    /// Progressively flushes a 2-second (or arbitrary length) 16 kHz 16-bit mono PCM chunk to the active segment WAV file
    /// with <see cref="FileOptions.WriteThrough"/>, updates <c>session_recording.pseg</c>, and automatically triggers
    /// segment rollover when the nominal segment duration (default 15 minutes, FR-REC-004) is reached.
    /// Also checks <see cref="StorageSpaceWatchdog"/> before writing (NFR-DATA-003).
    /// </summary>
    public async Task<CampaignHomeStatusDto> FlushPcmChunkAsync(
        string campaignId,
        string sessionId,
        ReadOnlyMemory<byte> pcmChunk,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        await _engineLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var storageStatus = _storageWatchdog.Evaluate(_walManager.GetCampaignDirectory(campaignId));
            var sessionDoc = await GetOrCreateSessionDocumentUnlockedAsync(campaignId, sessionId, cancellationToken).ConfigureAwait(false);

            if (storageStatus.IsCriticalStorageExhausted)
            {
                await CloseActiveSegmentsSafelyUnlockedAsync(
                    campaignId,
                    sessionId,
                    closureReason: "CriticalStorageExhausted",
                    cancellationToken).ConfigureAwait(false);

                _activeSessions.TryRemove(GetSessionKey(campaignId, sessionId), out _);

                var segmentsAfterStop = await _walManager.ReadEntriesAsync(campaignId, sessionId, cancellationToken).ConfigureAwait(false);
                sessionDoc.RecordingState = SessionRecordingState.Paused;
                sessionDoc.Segments = segmentsAfterStop.ToList();
                sessionDoc.TotalRecordedDurationMs = segmentsAfterStop.Sum(s => s.DurationMs);

                await _campaignMemoryStore.UpsertSessionFromWalAsync(campaignId, sessionDoc, cancellationToken).ConfigureAwait(false);
                _priorityScheduler?.NotifySessionStateChanged(SessionRecordingState.Paused);

                return await BuildCampaignHomeStatusUnlockedAsync(campaignId, sessionDoc, storageStatus, cancellationToken).ConfigureAwait(false);
            }

            var currentEntries = await _walManager.ReadEntriesAsync(campaignId, sessionId, cancellationToken).ConfigureAwait(false);
            var activeEntry = currentEntries.LastOrDefault(e => e.Status == AudioSegmentWalStatus.Active);

            if (activeEntry is null)
            {
                int nextSeq = currentEntries.Count == 0 ? 1 : currentEntries.Max(e => e.SequenceNumber) + 1;
                activeEntry = await OpenNewActiveSegmentUnlockedAsync(campaignId, sessionId, nextSeq, initialPcmBytes: 0, cancellationToken).ConfigureAwait(false);
            }

            // Append PCM bytes with FileOptions.WriteThrough and update WAV header sizes
            await AppendPcmBytesToSegmentFileAsync(activeEntry.FilePath, pcmChunk, cancellationToken).ConfigureAwait(false);

            var fileInfo = new FileInfo(activeEntry.FilePath);
            long totalPcmBytes = Math.Max(0L, fileInfo.Length - WavRiffHeaderHelper.HeaderSizeBytes);
            long durationMs = WavRiffHeaderHelper.CalculateDurationMs(totalPcmBytes);

            var updatedEntry = activeEntry with
            {
                LastFlushedAtUtc = DateTimeOffset.UtcNow,
                FlushedPcmBytes = totalPcmBytes,
                DurationMs = durationMs,
                Status = AudioSegmentWalStatus.Active
            };

            await _walManager.AppendOrUpdateEntryAsync(campaignId, sessionId, updatedEntry, cancellationToken).ConfigureAwait(false);

            // Check if nominal segment duration (default 15 minutes, FR-REC-004) has been reached
            long nominalDurationMs = await GetNominalSegmentDurationMsUnlockedAsync(cancellationToken).ConfigureAwait(false);
            if (durationMs >= nominalDurationMs)
            {
                WavRiffHeaderHelper.RepairUnclosedWavHeader(updatedEntry.FilePath);
                var closedRolloverEntry = updatedEntry with
                {
                    LastFlushedAtUtc = DateTimeOffset.UtcNow,
                    Status = AudioSegmentWalStatus.ClosedSafe,
                    ClosureReason = "NominalDurationRollover"
                };
                await _walManager.AppendOrUpdateEntryAsync(campaignId, sessionId, closedRolloverEntry, cancellationToken).ConfigureAwait(false);

                int nextSequence = updatedEntry.SequenceNumber + 1;
                var nextActive = await OpenNewActiveSegmentUnlockedAsync(
                    campaignId,
                    sessionId,
                    nextSequence,
                    initialPcmBytes: 0,
                    cancellationToken).ConfigureAwait(false);

                _activeSessions[GetSessionKey(campaignId, sessionId)] = new ActiveSessionRuntimeState(
                    CampaignId: campaignId,
                    SessionId: sessionId,
                    MicrophoneDeviceId: null,
                    ActiveSegment: nextActive);
            }
            else
            {
                _activeSessions[GetSessionKey(campaignId, sessionId)] = new ActiveSessionRuntimeState(
                    CampaignId: campaignId,
                    SessionId: sessionId,
                    MicrophoneDeviceId: null,
                    ActiveSegment: updatedEntry);
            }

            var allSegments = await _walManager.ReadEntriesAsync(campaignId, sessionId, cancellationToken).ConfigureAwait(false);
            sessionDoc.RecordingState = SessionRecordingState.Recording;
            sessionDoc.Segments = allSegments.ToList();
            sessionDoc.TotalRecordedDurationMs = allSegments.Sum(s => s.DurationMs);

            await _campaignMemoryStore.UpsertSessionFromWalAsync(campaignId, sessionDoc, cancellationToken).ConfigureAwait(false);

            return await BuildCampaignHomeStatusUnlockedAsync(campaignId, sessionDoc, storageStatus, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _engineLock.Release();
        }
    }

    /// <summary>
    /// Cleans up temporary session audio files ONLY when allowed:
    /// <c>KeepOriginalSessionAudio == false</c>, the session is ended, and both transcription and memory processing
    /// have completed (FR-REC-003, NFR-DATA-006).
    /// </summary>
    public async Task<bool> TryCleanupProcessedSessionAudioAsync(
        string campaignId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        await _engineLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_systemStore is not null)
            {
                var settings = await _systemStore.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
                if (settings.KeepOriginalSessionAudio)
                {
                    return false; // User opted in to retain original session audio (NFR-DATA-006)
                }
            }

            var sessionDoc = await _campaignMemoryStore.GetSessionAsync(campaignId, sessionId, cancellationToken).ConfigureAwait(false);
            if (sessionDoc is null)
            {
                return false;
            }

            if (sessionDoc.RecordingState != SessionRecordingState.Ended ||
                sessionDoc.TranscriptionState != SubsystemProcessingState.Completed ||
                sessionDoc.MemoryProcessingState != SubsystemProcessingState.Completed)
            {
                return false; // Never delete unprocessed or active session audio (FR-REC-003)
            }

            var entries = await _walManager.ReadEntriesAsync(campaignId, sessionId, cancellationToken).ConfigureAwait(false);
            if (entries.Any(e => e.Status == AudioSegmentWalStatus.Active))
            {
                return false;
            }

            foreach (var entry in entries)
            {
                if (File.Exists(entry.FilePath))
                {
                    File.Delete(entry.FilePath);
                }

                var cleanedEntry = entry with
                {
                    LastFlushedAtUtc = DateTimeOffset.UtcNow,
                    Status = AudioSegmentWalStatus.CleanedUpAfterProcessing,
                    ClosureReason = "CleanedUpAfterProcessingComplete"
                };

                await _walManager.AppendOrUpdateEntryAsync(campaignId, sessionId, cleanedEntry, cancellationToken).ConfigureAwait(false);
            }

            var updatedEntries = await _walManager.ReadEntriesAsync(campaignId, sessionId, cancellationToken).ConfigureAwait(false);
            sessionDoc.AudioCleanedUp = true;
            sessionDoc.Segments = updatedEntries.ToList();
            await _campaignMemoryStore.UpsertSessionFromWalAsync(campaignId, sessionDoc, cancellationToken).ConfigureAwait(false);

            return true;
        }
        finally
        {
            _engineLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _engineLock.Dispose();
    }

    private async Task<AudioSegmentWalEntry> OpenNewActiveSegmentUnlockedAsync(
        string campaignId,
        string sessionId,
        int sequenceNumber,
        int initialPcmBytes,
        CancellationToken cancellationToken)
    {
        string segmentId = $"seg-{sequenceNumber:D3}";
        string filePath = _walManager.GetSegmentFilePath(campaignId, sessionId, sequenceNumber);
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        int alignedInitialBytes = Math.Max(0, initialPcmBytes - (initialPcmBytes % WavRiffHeaderHelper.BlockAlign));
        byte[] header = WavRiffHeaderHelper.CreateWavHeader(alignedInitialBytes);

        await using (var stream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            options: FileOptions.WriteThrough | FileOptions.Asynchronous))
        {
            await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            if (alignedInitialBytes > 0)
            {
                byte[] initialBuffer = new byte[alignedInitialBytes];
                await stream.WriteAsync(initialBuffer, cancellationToken).ConfigureAwait(false);
            }
            stream.Flush(flushToDisk: true);
        }

        var nowUtc = DateTimeOffset.UtcNow;
        var entry = new AudioSegmentWalEntry(
            SessionId: sessionId,
            SegmentId: segmentId,
            SequenceNumber: sequenceNumber,
            FilePath: filePath,
            StartedAtUtc: nowUtc,
            LastFlushedAtUtc: nowUtc,
            FlushedPcmBytes: alignedInitialBytes,
            DurationMs: WavRiffHeaderHelper.CalculateDurationMs(alignedInitialBytes),
            Status: AudioSegmentWalStatus.Active,
            ClosureReason: null);

        await _walManager.AppendOrUpdateEntryAsync(campaignId, sessionId, entry, cancellationToken).ConfigureAwait(false);
        return entry;
    }

    private static async Task AppendPcmBytesToSegmentFileAsync(
        string filePath,
        ReadOnlyMemory<byte> pcmChunk,
        CancellationToken cancellationToken)
    {
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        bool exists = File.Exists(filePath);
        await using var stream = new FileStream(
            filePath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.Read,
            bufferSize: 4096,
            options: FileOptions.WriteThrough | FileOptions.Asynchronous);

        if (!exists || stream.Length < WavRiffHeaderHelper.HeaderSizeBytes)
        {
            byte[] header = WavRiffHeaderHelper.CreateWavHeader(pcmChunk.Length);
            stream.Seek(0, SeekOrigin.Begin);
            await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        }

        if (pcmChunk.Length > 0)
        {
            stream.Seek(0, SeekOrigin.End);
            await stream.WriteAsync(pcmChunk, cancellationToken).ConfigureAwait(false);
        }

        long totalPcmBytes = Math.Max(0L, stream.Length - WavRiffHeaderHelper.HeaderSizeBytes);
        WavRiffHeaderHelper.UpdateWavHeaderSizes(stream, totalPcmBytes);
        stream.Flush(flushToDisk: true);
    }

    private async Task CloseActiveSegmentsSafelyUnlockedAsync(
        string campaignId,
        string sessionId,
        string closureReason,
        CancellationToken cancellationToken)
    {
        var entries = await _walManager.ReadEntriesAsync(campaignId, sessionId, cancellationToken).ConfigureAwait(false);
        foreach (var entry in entries.Where(e => e.Status == AudioSegmentWalStatus.Active))
        {
            long durationMs = entry.DurationMs;
            long pcmBytes = entry.FlushedPcmBytes;

            if (File.Exists(entry.FilePath))
            {
                var fileInfo = new FileInfo(entry.FilePath);
                if (fileInfo.Length >= WavRiffHeaderHelper.HeaderSizeBytes)
                {
                    durationMs = WavRiffHeaderHelper.RepairUnclosedWavHeader(entry.FilePath);
                    pcmBytes = Math.Max(0L, fileInfo.Length - WavRiffHeaderHelper.HeaderSizeBytes);
                }
            }

            var closedEntry = entry with
            {
                LastFlushedAtUtc = DateTimeOffset.UtcNow,
                FlushedPcmBytes = pcmBytes,
                DurationMs = durationMs,
                Status = AudioSegmentWalStatus.ClosedSafe,
                ClosureReason = closureReason
            };

            await _walManager.AppendOrUpdateEntryAsync(campaignId, sessionId, closedEntry, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<SessionDocument> GetOrCreateSessionDocumentUnlockedAsync(
        string campaignId,
        string sessionId,
        CancellationToken cancellationToken)
    {
        var existing = await _campaignMemoryStore.GetSessionAsync(campaignId, sessionId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var allSessions = await _campaignMemoryStore.ListSessionsAsync(campaignId, cancellationToken).ConfigureAwait(false);
        int sessionNumber = allSessions.Count + 1;

        return new SessionDocument
        {
            Id = sessionId,
            CampaignId = campaignId,
            SessionNumber = sessionNumber,
            Title = $"Session {sessionNumber}",
            RecordingState = SessionRecordingState.Idle,
            TranscriptionState = SubsystemProcessingState.Pending,
            MemoryProcessingState = SubsystemProcessingState.Pending,
            StartedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private async Task<long> GetNominalSegmentDurationMsUnlockedAsync(CancellationToken cancellationToken)
    {
        int minutes = 15;
        if (_systemStore is not null)
        {
            var settings = await _systemStore.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
            if (settings.NominalSegmentDurationMinutes > 0)
            {
                minutes = settings.NominalSegmentDurationMinutes;
            }
        }
        return minutes * 60_000L;
    }

    private async Task<CampaignHomeStatusDto> BuildCampaignHomeStatusUnlockedAsync(
        string campaignId,
        SessionDocument? sessionDoc,
        StorageSpaceStatus storageStatus,
        CancellationToken cancellationToken)
    {
        var entities = await _campaignMemoryStore.QueryDmFullMemoryAsync(campaignId, cancellationToken: cancellationToken).ConfigureAwait(false);
        int needsAttentionCount = entities.Count(e =>
            e.Confidence == ConfidenceLevel.LowOrUncertain ||
            e.KnowledgeState == KnowledgeState.UnknownOrUnresolved ||
            e.Visibility == VisibilityScope.Ambiguous);

        bool isThrottled = _priorityScheduler?.IsBackgroundThrottled ?? false;
        string? throttleEn = _priorityScheduler?.ThrottlingReasonEn ?? storageStatus.WarningMessageEn;
        string? throttleEs = _priorityScheduler?.ThrottlingReasonEs ?? storageStatus.WarningMessageEs;

        if (sessionDoc is null)
        {
            return new CampaignHomeStatusDto(
                CampaignId: campaignId,
                ActiveSessionId: null,
                SessionState: SessionRecordingState.Idle,
                EonixVisualState: needsAttentionCount > 0 ? EonixState.NeedsAttention : EonixState.Idle,
                RecordingSaveStatus: SubsystemProcessingState.Idle,
                TranscriptionStatus: SubsystemProcessingState.Idle,
                MemoryProcessingStatus: SubsystemProcessingState.Idle,
                EonixQueryStatus: SubsystemProcessingState.Idle,
                TotalRecordedDurationMs: 0L,
                SafelyStoredSegmentsCount: 0,
                TranscribedSegmentsCount: 0,
                MemoryProcessedSegmentsCount: 0,
                NeedsAttentionCount: needsAttentionCount,
                FreeStorageGb: storageStatus.FreeStorageGb,
                IsStorageWarningActive: storageStatus.IsStorageWarningActive,
                IsBackgroundThrottled: isThrottled,
                ThrottlingReasonEn: throttleEn,
                ThrottlingReasonEs: throttleEs);
        }

        var segments = sessionDoc.Segments;
        int safelyStoredCount = segments.Count(s =>
            s.Status is AudioSegmentWalStatus.ClosedSafe
                or AudioSegmentWalStatus.RecoveredFromCrash
                or AudioSegmentWalStatus.CleanedUpAfterProcessing);

        int transcribedCount = sessionDoc.TranscriptionState == SubsystemProcessingState.Completed
            ? safelyStoredCount
            : 0;
        int memoryProcessedCount = sessionDoc.MemoryProcessingState == SubsystemProcessingState.Completed
            ? safelyStoredCount
            : 0;

        var recordingSaveStatus = sessionDoc.RecordingState switch
        {
            SessionRecordingState.Recording => SubsystemProcessingState.Processing,
            SessionRecordingState.Paused => SubsystemProcessingState.Saved,
            SessionRecordingState.Ended => SubsystemProcessingState.Saved,
            SessionRecordingState.Recovering => SubsystemProcessingState.Pending,
            _ => SubsystemProcessingState.Idle
        };

        var eonixState = sessionDoc.RecordingState switch
        {
            SessionRecordingState.Recording => EonixState.ListeningRecording,
            SessionRecordingState.Paused => storageStatus.IsCriticalStorageExhausted ? EonixState.NeedsAttention : EonixState.Paused,
            SessionRecordingState.Recovering => EonixState.NeedsAttention,
            SessionRecordingState.Ended when sessionDoc.TranscriptionState == SubsystemProcessingState.Processing => EonixState.Transcribing,
            SessionRecordingState.Ended when sessionDoc.MemoryProcessingState == SubsystemProcessingState.Processing => EonixState.MemoryProcessing,
            _ => needsAttentionCount > 0 ? EonixState.NeedsAttention : EonixState.Idle
        };

        string? activeSessionId = sessionDoc.RecordingState is SessionRecordingState.Recording or SessionRecordingState.Paused or SessionRecordingState.Recovering
            ? sessionDoc.Id
            : null;

        return new CampaignHomeStatusDto(
            CampaignId: campaignId,
            ActiveSessionId: activeSessionId,
            SessionState: sessionDoc.RecordingState,
            EonixVisualState: eonixState,
            RecordingSaveStatus: recordingSaveStatus,
            TranscriptionStatus: sessionDoc.TranscriptionState,
            MemoryProcessingStatus: sessionDoc.MemoryProcessingState,
            EonixQueryStatus: SubsystemProcessingState.Idle,
            TotalRecordedDurationMs: sessionDoc.TotalRecordedDurationMs,
            SafelyStoredSegmentsCount: safelyStoredCount,
            TranscribedSegmentsCount: transcribedCount,
            MemoryProcessedSegmentsCount: memoryProcessedCount,
            NeedsAttentionCount: needsAttentionCount,
            FreeStorageGb: storageStatus.FreeStorageGb,
            IsStorageWarningActive: storageStatus.IsStorageWarningActive,
            IsBackgroundThrottled: isThrottled,
            ThrottlingReasonEn: throttleEn,
            ThrottlingReasonEs: throttleEs);
    }

    private static string GetSessionKey(string campaignId, string sessionId) => $"{campaignId}::{sessionId}";

    private sealed record ActiveSessionRuntimeState(
        string CampaignId,
        string SessionId,
        string? MicrophoneDeviceId,
        AudioSegmentWalEntry ActiveSegment);
}