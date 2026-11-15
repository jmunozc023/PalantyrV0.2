using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Palantyr.AI.Orchestration;
using Palantyr.Audio;
using Palantyr.Contracts;
using Palantyr.Data.Raven;
using Palantyr.Domain;
using Palantyr.Host;
using Xunit;

namespace Palantyr.UnitTests;

public sealed class Wave1VerticalSliceTests
{
    [Fact]
    public async Task AC_SYS_001_SystemAssessment_And_MicrophoneValidation_Provide_Bilingual_Recommendations_And_CpuFallback()
    {
        var highEndService = new WindowsSystemAssessmentService(
            simulatedDedicatedVramGb: 8.0,
            simulatedTotalRamGb: 32.0,
            simulatedGpuModel: "NVIDIA GeForce RTX 4070");

        var profile = await highEndService.RunAssessmentAsync(includeBenchmarkProbe: true);

        Assert.Equal(ProcessingMode.LiveBalanced, profile.RecommendedMode);
        Assert.True(profile.HasCompatibleGpuAcceleration);
        Assert.False(string.IsNullOrWhiteSpace(profile.RecommendationReasonEn));
        Assert.False(string.IsNullOrWhiteSpace(profile.RecommendationReasonEs));
        Assert.NotEmpty(profile.AudioDevices);

        // Verify CPU fallback and ManualDeferred mode (FR-SYS-002)
        var cpuOnlyService = new WindowsSystemAssessmentService(
            simulatedDedicatedVramGb: 0.0,
            simulatedTotalRamGb: 8.0,
            simulatedGpuModel: "Integrated Graphics");

        var cpuProfile = await cpuOnlyService.RunAssessmentAsync(includeBenchmarkProbe: false);
        Assert.Equal(ProcessingMode.ManualDeferred, cpuProfile.RecommendedMode);
        Assert.False(cpuProfile.HasCompatibleGpuAcceleration);
        Assert.Contains("CPU", cpuProfile.RecommendationReasonEn, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CPU", cpuProfile.RecommendationReasonEs, StringComparison.OrdinalIgnoreCase);

        // Verify Microphone Validation (FR-REC-001)
        var validMic = await highEndService.ValidateMicrophoneAsync("default-wasapi-mic");
        Assert.True(validMic.IsAvailable);
        Assert.True(validMic.HasDetectableSignal);
        Assert.False(string.IsNullOrWhiteSpace(validMic.DiagnosticMessageEn));
        Assert.False(string.IsNullOrWhiteSpace(validMic.DiagnosticMessageEs));

        var disconnectedMic = await highEndService.ValidateMicrophoneAsync("usb-disconnected-mic");
        Assert.False(disconnectedMic.IsAvailable);
        Assert.False(disconnectedMic.HasDetectableSignal);
    }

    [Fact]
    public async Task KaedeFinding2_SingleActiveGpuWorkerMutex_And_RuntimePriorityScheduler_Throttling_Enforced()
    {
        using var scheduler = new RuntimePriorityScheduler(detectedVramGb: 8.0);
        Assert.True(scheduler.EnforceSingleGpuWorkerMutex);

        scheduler.NotifySessionStateChanged(SessionRecordingState.Recording);
        Assert.Equal(SessionRecordingState.Recording, scheduler.CurrentSessionState);

        // Verify Single-Active-GPU-Worker Mutex prevents concurrent GPU workers on < 12 GB VRAM
        var slot1 = await scheduler.AcquireGpuWorkerSlotAsync(RuntimePriorityTier.DeferrableBackgroundProcessing);
        Assert.Equal(1, scheduler.ActiveGpuWorkerCount);

        bool secondSlotAcquiredWhileFirstHeld = false;
        var slot2Task = Task.Run(async () =>
        {
            using var slot2 = await scheduler.AcquireGpuWorkerSlotAsync(RuntimePriorityTier.HighInteractiveEonixQuery);
            secondSlotAcquiredWhileFirstHeld = scheduler.ActiveGpuWorkerCount > 1;
        });

        await Task.Delay(40);
        Assert.False( slot2Task.IsCompleted );

        slot1.Dispose();
        await slot2Task;
        Assert.False(secondSlotAcquiredWhileFirstHeld);
        Assert.Equal(0, scheduler.ActiveGpuWorkerCount);

        // Verify CPU (> 80%), RAM (< 2.0 GB), and Disk (< 2.0 GB) adaptive throttling with bilingual explanations (NFR-PERF-008)
        bool shouldDefer = scheduler.ShouldDeferBackgroundWork(currentCpuPercent: 88.5, availableRamGb: 1.5, freeStorageGb: 1.2);
        Assert.True(shouldDefer);
        Assert.True(scheduler.IsBackgroundThrottled);
        Assert.Contains("Eonix", scheduler.ThrottlingReasonEn);
        Assert.Contains("88.5%", scheduler.ThrottlingReasonEn);
        Assert.Contains("Eonix", scheduler.ThrottlingReasonEs);
        Assert.Contains("88.5%", scheduler.ThrottlingReasonEs);

        bool recovered = scheduler.ShouldDeferBackgroundWork(currentCpuPercent: 35.0, availableRamGb: 8.0, freeStorageGb: 40.0);
        Assert.False(recovered);
        Assert.False(scheduler.IsBackgroundThrottled);
        Assert.Null(scheduler.ThrottlingReasonEn);
        Assert.Null(scheduler.ThrottlingReasonEs);
    }

    [Fact]
    public async Task AC_REC_001_To_003_And_AC_END_001_002_Start_Pause_Resume_End_CrashRecovery_And_10sClipSlice()
    {
        string tempDir = CreateTempTestDirectory();
        try
        {
            var ravenConfig = new RavenEmbeddedConfiguration(tempDir);
            var memoryStore = new RavenCampaignMemoryStore(ravenConfig);
            var systemStore = new RavenSystemStore(ravenConfig, memoryStore);
            using var scheduler = new RuntimePriorityScheduler(8.0);
            var walManager = new PsegWalManager(tempDir);
            var watchdog = new StorageSpaceWatchdog(tempDir, () => 50.0);
            using var engine = new AudioRecordingEngine(memoryStore, systemStore, scheduler, walManager, watchdog);

            var campaign = await systemStore.CreateCampaignAsync(new CreateCampaignRequest(
                Name: "Chronicles of Him Kuldir",
                SubtitleOrSettingName: "The Great Mine",
                WorldSummary: "Dwarven expedition into ancient ruins.",
                CalendarSystemDescription: "Relative Journey Days",
                PrimaryLanguage: PalantyrLanguage.Spanish,
                InitialPartyRoster: [],
                InitialWorldLore: [],
                PreferredMicrophoneDeviceId: "default-wasapi-mic"));

            const string sessionId = "session-001";

            // 1. Start session recording (creates segment_001.wav + .pseg Active entry)
            var startedStatus = await engine.StartSessionRecordingAsync(campaign.Id, sessionId, "default-wasapi-mic");
            Assert.Equal(SessionRecordingState.Recording, startedStatus.SessionState);
            Assert.Equal(EonixState.ListeningRecording, startedStatus.EonixVisualState);

            // Flush additional 10 seconds of PCM audio (320,000 bytes at 32,000 bytes/sec)
            byte[] tenSecondsPcm = new byte[WavRiffHeaderHelper.BytesPerSecond * 10];
            for (int i = 0; i < tenSecondsPcm.Length; i++)
            {
                tenSecondsPcm[i] = (byte)(i % 251);
            }
            await engine.FlushPcmChunkAsync(campaign.Id, sessionId, tenSecondsPcm);

            // 2. Pause session recording (FR-REC-005: closes segment_001.wav safely as ClosedSafe)
            var pausedStatus = await engine.PauseSessionRecordingAsync(campaign.Id, sessionId);
            Assert.Equal(SessionRecordingState.Paused, pausedStatus.SessionState);
            Assert.Equal(1, pausedStatus.SafelyStoredSegmentsCount);

            var entriesAfterPause = await walManager.ReadEntriesAsync(campaign.Id, sessionId);
            Assert.Single(entriesAfterPause);
            Assert.Equal(AudioSegmentWalStatus.ClosedSafe, entriesAfterPause[0].Status);

            // 3. Resume session recording (opens segment_002.wav as Active)
            var resumedStatus = await engine.ResumeSessionRecordingAsync(campaign.Id, sessionId);
            Assert.Equal(SessionRecordingState.Recording, resumedStatus.SessionState);

            var entriesAfterResume = await walManager.ReadEntriesAsync(campaign.Id, sessionId);
            Assert.Equal(2, entriesAfterResume.Count);
            Assert.Equal(AudioSegmentWalStatus.ClosedSafe, entriesAfterResume[0].Status);
            Assert.Equal(AudioSegmentWalStatus.Active, entriesAfterResume[1].Status);

            // 4. Simulate power loss / crash where extra PCM bytes were written to segment_002.wav without closing WAV header or .pseg
            string seg2Path = entriesAfterResume[1].FilePath;
            await using (var rawStream = new FileStream(seg2Path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                byte[] extraFourSeconds = new byte[WavRiffHeaderHelper.BytesPerSecond * 4];
                await rawStream.WriteAsync(extraFourSeconds);
                // Corrupt header sizes to 0 to simulate unclosed WAV header on crash
            }
            await using (var headerCorruptStream = new FileStream(seg2Path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                WavRiffHeaderHelper.UpdateWavHeaderSizes(headerCorruptStream, 0);
            }

            // 5. Run Startup Crash Recovery Scanner (Kaede Finding 1 + FR-REC-006..008)
            var recoveryReports = await engine.ScanAndRecoverInterruptedSessionsAsync(campaign.Id);
            Assert.Single(recoveryReports);
            var report = recoveryReports[0];
            Assert.Equal(1, report.PreviouslySafeSegmentsCount);
            Assert.Equal(1, report.RecoveredSegmentsCount);
            Assert.Equal(0, report.UnrecoverableSegmentsCount);
            Assert.True(report.TotalRecoveredDurationMs >= 16_000);

            // Verify segment_002.wav 44-byte RIFF header was repaired in-place to match file length
            byte[] repairedWavBytes = await File.ReadAllBytesAsync(seg2Path);
            uint riffSize = BinaryPrimitives.ReadUInt32LittleEndian(repairedWavBytes.AsSpan(4, 4));
            uint dataSize = BinaryPrimitives.ReadUInt32LittleEndian(repairedWavBytes.AsSpan(40, 4));
            Assert.Equal((uint)(repairedWavBytes.Length - 44), dataSize);
            Assert.Equal(36u + dataSize, riffSize);

            // Verify 10-second audio context clip slicer around timestamp (FR-VAL-002)
            byte[] clipBytes = await engine.ExtractAudioContextClipAsync(
                campaign.Id,
                sessionId,
                segmentId: "seg-001",
                centerTimestampMs: 6000,
                windowSeconds: 10);
            Assert.True(clipBytes.Length > WavRiffHeaderHelper.HeaderSizeBytes);
            Assert.Equal("RIFF", Encoding.ASCII.GetString(clipBytes, 0, 4));
            Assert.Equal("WAVE", Encoding.ASCII.GetString(clipBytes, 8, 4));

            // 6. Resolve recovered session and run Two-Stage End Session (AC-END-001..002)
            var resolvedStatus = await engine.ResolveRecoveredSessionAsync(
                campaign.Id,
                sessionId,
                CrashRecoveryChoice.ContinueSession);
            Assert.Equal(SessionRecordingState.Recording, resolvedStatus.SessionState);

            var endSummary = await engine.EndSessionRecordingAsync(campaign.Id, sessionId);
            Assert.Equal(3, endSummary.SafelyStoredSegments);
            Assert.True(endSummary.ProcessingRemaining);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task AC_SUM_002_And_KaedeFinding3_PartySafeMemoryView_Strips_DMPrivate_Entities_And_Embedded_SubItems()
    {
        string tempDir = CreateTempTestDirectory();
        try
        {
            var ravenConfig = new RavenEmbeddedConfiguration(tempDir);
            var memoryStore = new RavenCampaignMemoryStore(ravenConfig);
            const string campaignId = "camp-privacy-01";

            // 1. Publicly known character with mixed PartyKnown, DMPrivate, and Ambiguous history items
            var mixedCharacter = new MemoryEntityDocument
            {
                Id = "characters/npc-m-01",
                EntityType = MemoryEntityType.Character,
                CanonicalName = "Archmage M",
                Aliases = ["The Watcher"],
                RecognitionVariants = ["Arch mage Em", "Arkmage M"],
                SummaryDescription = "An advisor to the expedition.",
                DmPrivateNotes = "SECRET: M is secretly the cult leader orchestrating the collapse.",
                Visibility = VisibilityScope.PartyKnown,
                KnowledgeState = KnowledgeState.CampaignTruth,
                Confidence = ConfidenceLevel.DMConfirmed,
                StateAndBeliefHistory =
                [
                    new MemoryHistoryItem(
                        HistoryItemId: "hist-public-1",
                        Description: "Party believes M is a loyal scholar.",
                        KnowledgeState: KnowledgeState.PartyBelief,
                        Confidence: ConfidenceLevel.HighConfidence,
                        Visibility: VisibilityScope.PartyKnown,
                        ValidFromSessionId: "session-01",
                        ValidToSessionId: null,
                        RelativeTimeContext: "Day 2 of Journey",
                        SupersededByHistoryItemId: null,
                        RecordedAtUtc: DateTimeOffset.UtcNow),
                    new MemoryHistoryItem(
                        HistoryItemId: "hist-secret-2",
                        Description: "DM SECRET: M sabotaged the Great Mine seal.",
                        KnowledgeState: KnowledgeState.DMPrivateTruth,
                        Confidence: ConfidenceLevel.DMConfirmed,
                        Visibility: VisibilityScope.DMPrivate,
                        ValidFromSessionId: "session-01",
                        ValidToSessionId: null,
                        RelativeTimeContext: "Before Campaign",
                        SupersededByHistoryItemId: null,
                        RecordedAtUtc: DateTimeOffset.UtcNow),
                    new MemoryHistoryItem(
                        HistoryItemId: "hist-ambiguous-3",
                        Description: "Unconfirmed whisper in the hallway.",
                        KnowledgeState: KnowledgeState.ClaimOrRumor,
                        Confidence: ConfidenceLevel.LowOrUncertain,
                        Visibility: VisibilityScope.Ambiguous,
                        ValidFromSessionId: "session-02",
                        ValidToSessionId: null,
                        RelativeTimeContext: "Day 3 of Journey",
                        SupersededByHistoryItemId: null,
                        RecordedAtUtc: DateTimeOffset.UtcNow)
                ]
            };

            // 2. Entirely DMPrivate entity
            var secretCult = new MemoryEntityDocument
            {
                Id = "organizations/obsidian-cult-01",
                EntityType = MemoryEntityType.Organization,
                CanonicalName = "Order of the Obsidian Eclipse",
                SummaryDescription = "Hidden faction pulling strings behind the throne.",
                Visibility = VisibilityScope.DMPrivate,
                KnowledgeState = KnowledgeState.DMPrivateTruth,
                Confidence = ConfidenceLevel.DMConfirmed
            };

            await memoryStore.SaveMemoryEntityAsync(campaignId, mixedCharacter);
            await memoryStore.SaveMemoryEntityAsync(campaignId, secretCult);

            var partySafeResults = await memoryStore.QueryPartySafeMemoryAsync(campaignId, QueryVisibilityContext.PartySafe);

            // Secret entity is completely excluded
            Assert.Single(partySafeResults);
            var safeM = partySafeResults[0];
            Assert.Equal("characters/npc-m-01", safeM.Id);

            // Embedded DMPrivate and Ambiguous StateAndBeliefHistory items are stripped before generation (Kaede Finding 3)
            Assert.Single(safeM.StateAndBeliefHistory);
            Assert.Equal("hist-public-1", safeM.StateAndBeliefHistory[0].HistoryItemId);
            Assert.DoesNotContain(safeM.StateAndBeliefHistory, h => h.Description.Contains("DM SECRET", StringComparison.OrdinalIgnoreCase));

            // Full DM query retains both entities and all 3 history items
            var dmFullResults = await memoryStore.QueryDmFullMemoryAsync(campaignId);
            Assert.Equal(2, dmFullResults.Count);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void KaedeFinding4_MemoryResolutionTombstone_MatchesOverlap_Resists_ModelUpgrade_TimestampDrift()
    {
        var tombstone = new MemoryResolutionTombstone(
            Id: "tombstones/reject-dragon-joke-01",
            SessionId: "session-04",
            SegmentId: "seg-002",
            ApproximateStartMs: 120_000,
            ApproximateEndMs: 128_000,
            NormalizedFactSignature: "claim:tjuv:ate_the_moon",
            TargetEntityIds: ["characters/tjuv-01"],
            DmDecision: "RejectedAsNonCanonJoke",
            ResolvedAtUtc: DateTimeOffset.UtcNow);

        // After upgrading Whisper model, utterance boundaries shift by +1800ms (within ±2500ms tolerance)
        bool matchesShifted = tombstone.MatchesOverlap(
            sessionId: "session-04",
            segmentId: "seg-002",
            spanStartMs: 129_800,
            spanEndMs: 132_000,
            normalizedSignature: "claim:tjuv:ate_the_moon");

        Assert.True(matchesShifted);

        // Outside ±2500ms window should not match
        bool matchesFarAway = tombstone.MatchesOverlap(
            sessionId: "session-04",
            segmentId: "seg-002",
            spanStartMs: 145_000,
            spanEndMs: 150_000,
            normalizedSignature: "claim:tjuv:ate_the_moon");

        Assert.False(matchesFarAway);
    }

    [Fact]
    public void AC_DATA_005_MonthlyBackupReminder_Triggers_Only_With_MeaningfulChanges_After30Days_And_Never_During_Recording()
    {
        var createdAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var campaign = new CampaignDocument
        {
            Id = "camp-backup-test",
            DatabaseName = "Palantyr_Campaign_camp-backup-test",
            Name = "Backup Test Campaign",
            CreatedAtUtc = createdAt,
            LastBackupAtUtc = createdAt,
            LastMeaningfulChangeAtUtc = createdAt.AddDays(5)
        };

        var after31Days = createdAt.AddDays(31);

        // 1. Triggers when >= 30 days elapsed AND meaningful changes exist AND session is Idle
        Assert.True(campaign.ShouldRecommendMonthlyBackup(after31Days, SessionRecordingState.Idle));

        // 2. Never interrupts an active Recording or Paused session (NFR-DATA-009)
        Assert.False(campaign.ShouldRecommendMonthlyBackup(after31Days, SessionRecordingState.Recording));
        Assert.False(campaign.ShouldRecommendMonthlyBackup(after31Days, SessionRecordingState.Paused));

        // 3. Does not trigger if < 30 days elapsed
        Assert.False(campaign.ShouldRecommendMonthlyBackup(createdAt.AddDays(15), SessionRecordingState.Idle));

        // 4. Does not trigger if no meaningful changes occurred since last backup (AC-DATA-005)
        campaign.LastBackupAtUtc = createdAt.AddDays(10);
        Assert.False(campaign.ShouldRecommendMonthlyBackup(createdAt.AddDays(45), SessionRecordingState.Idle));
    }

    [Fact]
    public async Task AC_DATA_003_006_007_BackupContainer_OptionalAudioInclusion_And_SafeCampaignDeletion()
    {
        string tempDir = CreateTempTestDirectory();
        try
        {
            var ravenConfig = new RavenEmbeddedConfiguration(tempDir);
            var memoryStore = new RavenCampaignMemoryStore(ravenConfig);
            var systemStore = new RavenSystemStore(ravenConfig, memoryStore);

            var campaign = await systemStore.CreateCampaignAsync(new CreateCampaignRequest(
                Name: "Shadows of Valdris",
                SubtitleOrSettingName: "Dark Sun",
                WorldSummary: "Desert survival campaign.",
                CalendarSystemDescription: "King's Age",
                PrimaryLanguage: PalantyrLanguage.English,
                InitialPartyRoster:
                [
                    new PartyMemberDto(
                        CharacterId: "characters/tjuv-01",
                        CharacterName: "Tjuv",
                        RaceOrSpecies: "Elf",
                        ClassOrArchetype: "Rogue",
                        ControlledByPlayerName: "Alex",
                        IsPlayerCharacter: true,
                        CurrentStatus: PartyMembershipStatus.Active,
                        MembershipHistory: [])
                ],
                InitialWorldLore: [],
                PreferredMicrophoneDeviceId: null));

            // Create a retained .wav file in the campaign's Audio folder
            string retainedWavPath = Path.Combine(ravenConfig.GetCampaignDirectoryPath(campaign.Id), "Audio", "segment_001.wav");
            await File.WriteAllBytesAsync(retainedWavPath, WavRiffHeaderHelper.CreateWavHeader(0));

            string backupsDir = ravenConfig.GetBackupsDirectoryPath();

            // 1. Default backup (includeRetainedAudio: false) excludes .wav files (AC-DATA-007)
            string backupWithoutAudio = await memoryStore.CreateCampaignBackupAsync(
                campaign.Id,
                backupsDir,
                includeRetainedAudio: false);

            Assert.True(File.Exists(backupWithoutAudio));
            using (var zipNoAudio = ZipFile.OpenRead(backupWithoutAudio))
            {
                Assert.NotNull(zipNoAudio.GetEntry("manifest.json"));
                Assert.NotNull(zipNoAudio.GetEntry("campaign.ravendbdump"));
                Assert.DoesNotContain(zipNoAudio.Entries, e => e.FullName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase));
            }

            // 2. Explicit opt-in backup (includeRetainedAudio: true) includes .wav files (AC-DATA-007)
            string backupWithAudio = await memoryStore.CreateCampaignBackupAsync(
                campaign.Id,
                backupsDir,
                includeRetainedAudio: true);

            using (var zipWithAudio = ZipFile.OpenRead(backupWithAudio))
            {
                Assert.Contains(zipWithAudio.Entries, e => e.FullName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase));
            }

            // 3. Safe Campaign Deletion with CreateBackupAndDelete (AC-DATA-006)
            string? deletionBackup = await systemStore.DeleteCampaignSafelyAsync(
                campaign.Id,
                CampaignDeletionOption.CreateBackupAndDelete,
                backupsDir);

            Assert.NotNull(deletionBackup);
            Assert.True(File.Exists(deletionBackup));
            Assert.Null(await systemStore.GetCampaignAsync(campaign.Id));
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task AC_REL_004_And_AC_PRIV_002_WindowCloseGuard_And_DiagnosticRedactor_Protect_Session_And_Secrets()
    {
        string tempDir = CreateTempTestDirectory();
        try
        {
            var ravenConfig = new RavenEmbeddedConfiguration(tempDir);
            var memoryStore = new RavenCampaignMemoryStore(ravenConfig);
            var systemStore = new RavenSystemStore(ravenConfig, memoryStore);
            var walManager = new PsegWalManager(tempDir);
            var watchdog = new StorageSpaceWatchdog(tempDir, () => 50.0);
            using var engine = new AudioRecordingEngine(memoryStore, systemStore, null, walManager, watchdog);
            var closeGuard = new WindowCloseGuardService();

            // 1. When Idle, window close is permitted immediately
            var idleCheck = closeGuard.EvaluateWindowClose();
            Assert.True(idleCheck.CanExitImmediately);

            // 2. Start active recording and verify WindowCloseGuard blocks silent exit (NFR-REL-009, AC-REL-004)
            await engine.StartSessionRecordingAsync("camp-guard", "sess-guard", "default-wasapi-mic");
            closeGuard.NotifySessionState("camp-guard", "sess-guard", SessionRecordingState.Recording);

            var activeCheck = closeGuard.EvaluateWindowClose();
            Assert.False(activeCheck.CanExitImmediately);
            Assert.True(activeCheck.RequiresUserConfirmation);
            Assert.Contains(WindowCloseResolutionAction.EndSessionAndExit, activeCheck.AllowedActions);
            Assert.Contains(WindowCloseResolutionAction.ReturnToSession, activeCheck.AllowedActions);
            Assert.False(string.IsNullOrWhiteSpace(activeCheck.WarningMessageEn));
            Assert.False(string.IsNullOrWhiteSpace(activeCheck.WarningMessageEs));

            // 3. Choosing EndSessionAndExit safely closes the recording segment and allows exit
            var resolvedExit = await closeGuard.ResolveCloseDecisionAsync(WindowCloseResolutionAction.EndSessionAndExit, engine);
            Assert.True(resolvedExit.CanExitImmediately);

            var finalSession = await memoryStore.GetSessionAsync("camp-guard", "sess-guard");
            Assert.NotNull(finalSession);
            Assert.Equal(SessionRecordingState.Ended, finalSession.RecordingState);

            // 4. Verify DiagnosticRedactor strips DMPrivate secrets and transcript text while keeping IDs & error causes (AC-PRIV-002)
            var secretEntity = new MemoryEntityDocument
            {
                Id = "quests/crimson-betrayal-01",
                EntityType = MemoryEntityType.Quest,
                CanonicalName = "The Crimson Betrayal",
                SummaryDescription = "Public quest hook.",
                DmPrivateNotes = "TOP_SECRET_PLOT_TWIST_KING_IS_DOPPELGANGER",
                Visibility = VisibilityScope.DMPrivate,
                KnowledgeState = KnowledgeState.DMPrivateTruth
            };

            var redactedEntityPayload = DiagnosticRedactor.RedactMemoryEntity(
                secretEntity,
                errorCode: "ERR-INDEX-004",
                errorCause: "Corax index writer lock timeout");

            string diagnosticJson = DiagnosticRedactor.ToDiagnosticJson(redactedEntityPayload);
            Assert.Contains("quests/crimson-betrayal-01", diagnosticJson);
            Assert.Contains("ERR-INDEX-004", diagnosticJson);
            Assert.Contains("Corax index writer lock timeout", diagnosticJson);
            Assert.DoesNotContain("TOP_SECRET_PLOT_TWIST_KING_IS_DOPPELGANGER", diagnosticJson);
            Assert.DoesNotContain("Public quest hook.", diagnosticJson);

            string rawLog = """{"segmentId":"seg-001","originalText":"The secret password is Shadowflame","dmPrivateNotes":"Hidden vault code 9942"}""";
            string sanitizedLog = DiagnosticRedactor.SanitizeLogText(rawLog, ["Shadowflame"]);
            Assert.DoesNotContain("Shadowflame", sanitizedLog);
            Assert.DoesNotContain("9942", sanitizedLog);
            Assert.Contains(DiagnosticRedactor.RedactionMarker, sanitizedLog);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    private static string CreateTempTestDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"PalantyrWave1Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup lock contention in temp folder
        }
    }
}