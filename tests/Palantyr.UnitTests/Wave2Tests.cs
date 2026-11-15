using Palantyr.AI.Orchestration;
using Palantyr.Contracts;
using Palantyr.Data.Raven;
using Palantyr.Domain;
using Xunit;

namespace Palantyr.UnitTests;

public sealed class Wave2Tests
{
    [Fact]
    public async Task AC_TRN_001_002_And_US_04_1_04_2_LocalWhisper_Transcribes_English_And_Spanish_With_Glossary_And_Lineage()
    {
        var engine = new LocalWhisperTranscriptionEngine();

        // 1. Spanish Transcription with fantasy glossary injection (FR-TRN-003, FR-MEM-CHAR-002)
        var spanishGlossary = new[] { "Him Kuldir", "Kharad-Zhul", "Moradin" };
        var spanishResult = await engine.TranscribeSegmentAsync(
            audioFilePath: "test_segment_es.wav",
            language: PalantyrLanguage.Spanish,
            fantasyGlossary: spanishGlossary);

        Assert.Equal("test_segment_es", spanishResult.SegmentId);
        Assert.Equal(PalantyrLanguage.Spanish, spanishResult.Language);
        Assert.NotEmpty(spanishResult.Utterances);
        Assert.Contains("Him Kuldir", spanishResult.Utterances[0].Text);
        Assert.All(spanishResult.Utterances, u =>
        {
            Assert.True(u.StartMs >= 0);
            Assert.True(u.EndMs > u.StartMs);
            Assert.True(u.ConfidenceScore > 0.80);
            Assert.False(string.IsNullOrWhiteSpace(u.Text));
        });

        Assert.NotNull(spanishResult.Lineage);
        Assert.Equal(LocalWhisperTranscriptionEngine.DefaultEngineName, spanishResult.Lineage.Engine);
        Assert.Equal(LocalWhisperTranscriptionEngine.DefaultModelId, spanishResult.Lineage.ModelId);
        Assert.False(string.IsNullOrWhiteSpace(spanishResult.Lineage.PromptTemplateHash));

        // 2. English Transcription with glossary
        var englishGlossary = new[] { "Valdris", "Aeloria" };
        var englishResult = await engine.TranscribeSegmentAsync(
            audioFilePath: "test_segment_en.wav",
            language: PalantyrLanguage.English,
            fantasyGlossary: englishGlossary);

        Assert.Equal(PalantyrLanguage.English, englishResult.Language);
        Assert.NotEmpty(englishResult.Utterances);
        Assert.Contains("Valdris", englishResult.Utterances[0].Text);
        Assert.True(englishResult.Utterances[0].ConfidenceScore > 0.85);
    }

    [Fact]
    public async Task US_04_3_PersistentTranscriptionQueue_Enforces_Scheduler_Throttling_And_Stores_DualTranscript()
    {
        string tempDir = CreateTempTestDirectory();
        try
        {
            var ravenConfig = new RavenEmbeddedConfiguration(tempDir);
            var memoryStore = new RavenCampaignMemoryStore(ravenConfig);
            using var scheduler = new RuntimePriorityScheduler(8.0);
            var engine = new LocalWhisperTranscriptionEngine();
            var classifier = new TwoStageContextClassifier();
            var queue = new PersistentTranscriptionQueue(engine, classifier, memoryStore, scheduler);

            const string campaignId = "camp-queue-test";
            const string sessionId = "session-01";
            const string segmentId = "seg-001";

            // 1. Enqueue job
            queue.EnqueueSegment(new TranscriptionJobRequest(
                CampaignId: campaignId,
                SessionId: sessionId,
                SegmentId: segmentId,
                AudioFilePath: "fake_session_audio.wav",
                Language: PalantyrLanguage.Spanish,
                FantasyGlossary: ["Barovian Mist"]));

            Assert.Equal(1, queue.PendingCount);

            // 2. Simulate resource throttle (e.g. GPU / CPU pressure from 3D VTT)
            scheduler.ShouldDeferBackgroundWork(currentCpuPercent: 89.0, availableRamGb: 1.0, freeStorageGb: 5.0);
            Assert.True(scheduler.IsBackgroundThrottled);

            // Queue respects throttling and defers execution
            var deferredResult = await queue.ProcessNextJobAsync();
            Assert.Null(deferredResult);
            Assert.Equal(1, queue.PendingCount);

            // 3. System recovers from load
            scheduler.ShouldDeferBackgroundWork(currentCpuPercent: 30.0, availableRamGb: 8.0, freeStorageGb: 40.0);
            Assert.False(scheduler.IsBackgroundThrottled);

            // Now job processes successfully
            var successResult = await queue.ProcessNextJobAsync();
            Assert.NotNull(successResult);
            Assert.True(successResult.Success);
            Assert.Equal(0, queue.PendingCount);

            // 4. Verify Dual-Layer Transcript saved in RavenDB
            var savedDoc = await memoryStore.GetTranscriptSegmentAsync(campaignId, sessionId, segmentId);
            Assert.NotNull(savedDoc);
            Assert.Equal(segmentId, savedDoc.SegmentId);
            Assert.NotEmpty(savedDoc.MachineUtterances);
            Assert.NotEmpty(savedDoc.WorkingSpans);
            Assert.Equal(savedDoc.MachineUtterances.Count, savedDoc.WorkingSpans.Count);

            // Verify queue status DTO
            var queueStatuses = queue.GetQueueStatus(campaignId, sessionId);
            Assert.Single(queueStatuses);
            Assert.Equal(SubsystemProcessingState.Completed, queueStatuses[0].State);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task US_04_4_DualTranscript_Preserves_Raw_Machine_Utterance_When_Corrected()
    {
        string tempDir = CreateTempTestDirectory();
        try
        {
            var ravenConfig = new RavenEmbeddedConfiguration(tempDir);
            var memoryStore = new RavenCampaignMemoryStore(ravenConfig);

            const string campaignId = "camp-dual-test";
            const string sessionId = "sess-01";
            const string segmentId = "seg-01";

            var transcriptDoc = new TranscriptSegmentDocument
            {
                Id = $"transcripts/{campaignId}/{sessionId}/{segmentId}",
                CampaignId = campaignId,
                SessionId = sessionId,
                SegmentId = segmentId,
                SequenceNumber = 1,
                Language = PalantyrLanguage.Spanish,
                MachineUtterances =
                [
                    new MachineUtterance("utt-1", 0, 3000, "El orco ataca a Tjub", 0.78)
                ],
                WorkingSpans =
                [
                    new WorkingTranscriptSpan
                    {
                        SpanId = "span-1",
                        SegmentId = segmentId,
                        StartMs = 0,
                        EndMs = 3000,
                        OriginalText = "El orco ataca a Tjub",
                        CorrectedText = "El orco ataca a Tjub",
                        ContextType = ConversationContextType.InWorldCanonCandidate,
                        ContextConfidence = 0.90
                    }
                ]
            };

            await memoryStore.SaveTranscriptSegmentAsync(campaignId, transcriptDoc);

            // DM corrects spelling of name "Tjub" -> "Tjuv" (FR-COR-001)
            var loaded = await memoryStore.GetTranscriptSegmentAsync(campaignId, sessionId, segmentId);
            Assert.NotNull(loaded);
            loaded.ApplyCorrection("span-1", "El orco ataca a Tjuv", "DM_Alex");
            await memoryStore.SaveTranscriptSegmentAsync(campaignId, loaded);

            var updated = await memoryStore.GetTranscriptSegmentAsync(campaignId, sessionId, segmentId);
            Assert.NotNull(updated);

            // Layer 1 (Machine) is strictly immutable
            Assert.Equal("El orco ataca a Tjub", updated.MachineUtterances[0].OriginalText);

            // Layer 2 (Working) holds DM correction
            Assert.Equal("El orco ataca a Tjuv", updated.WorkingSpans[0].CorrectedText);
            Assert.True(updated.WorkingSpans[0].IsUserEdited);
            Assert.Equal("DM_Alex", updated.WorkingSpans[0].EditedBy);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task AC_CTX_001_To_005_TwoStageClassifier_Accurately_Classifies_Context_Spans()
    {
        var classifier = new TwoStageContextClassifier();

        var utterances = new List<TranscriptUtteranceDto>
        {
            // Rules & Meta (EN)
            new("utt-1", 0, 3000, "Everyone roll for initiative and check your armor class.", 0.95),
            // Out of Character Table Talk (ES)
            new("utt-2", 3100, 6000, "¿Quién va a pedir la pizza y las cervezas para el intermedio?", 0.94),
            // In-World Canon Candidate (ES)
            new("utt-3", 6100, 11000, "El anciano enano abre la compuerta de hierro forjado.", 0.92),
            // Hypothetical Joke without ratification (EN)
            new("utt-4", 11100, 15000, "What if I fireball the tavern and burn the shopkeeper haha", 0.90),
            // Ambiguous query
            new("utt-5", 15100, 18000, "¿Tal vez?", 0.80)
        };

        var classified = await classifier.ClassifySpansAsync("camp-1", "sess-1", "seg-1", utterances);
        Assert.Equal(5, classified.Count);

        Assert.Equal(ConversationContextType.RulesOrMeta, classified[0].ContextType);
        Assert.True(classified[0].ContextConfidence >= 0.90);

        Assert.Equal(ConversationContextType.OutOfCharacterTableTalk, classified[1].ContextType);
        Assert.True(classified[1].ContextConfidence >= 0.88);

        Assert.Equal(ConversationContextType.InWorldCanonCandidate, classified[2].ContextType);

        Assert.Equal(ConversationContextType.HypotheticalOrJoke, classified[3].ContextType);

        Assert.Equal(ConversationContextType.Ambiguous, classified[4].ContextType);
    }

    [Fact]
    public async Task US_05_2_And_FR_CTX_006_JokeToCanon_Transition_Promotes_To_InWorldCanon()
    {
        var classifier = new TwoStageContextClassifier();

        // Utterance sequence where player makes a joke, immediately ratified by following utterance
        var utterances = new List<TranscriptUtteranceDto>
        {
            new("utt-1", 0, 4000, "Qué pasaría si lanzo bola de fuego a la taberna jaja", 0.92),
            new("utt-2", 4100, 7000, "Espera, no, en serio hago eso. Gasto el espacio de nivel 3.", 0.95)
        };

        var classified = await classifier.ClassifySpansAsync("camp-1", "sess-1", "seg-1", utterances);
        Assert.Equal(2, classified.Count);

        // Joke span promoted to InWorldCanonCandidate with ratification provenance (FR-CTX-006)
        Assert.Equal(ConversationContextType.InWorldCanonCandidate, classified[0].ContextType);
        Assert.True(classified[0].IsManuallyConfirmed);
        Assert.Contains("Joke-to-Canon", classified[0].PromotionReason);

        // Manual promotion test
        var jokeSpan = new WorkingTranscriptSpanDto(
            SpanId: "span-joke",
            SegmentId: "seg-1",
            StartMs: 0,
            EndMs: 2000,
            CorrectedText: "I steal the king's crown haha",
            OriginalText: "I steal the king's crown haha",
            ContextType: ConversationContextType.HypotheticalOrJoke,
            ContextConfidence: 0.85,
            IsManuallyConfirmed: false);

        var promoted = classifier.PromoteJokeToCanon(jokeSpan, "DM ruled: Rogue really pickpocketed the crown", "DM_JP");
        Assert.Equal(ConversationContextType.InWorldCanonCandidate, promoted.ContextType);
        Assert.True(promoted.IsManuallyConfirmed);
        Assert.Equal("DM_JP", promoted.ConfirmedBy);
    }

    [Fact]
    public async Task US_05_3_Ambiguous_Context_Generates_NeedsAttention_Item()
    {
        string tempDir = CreateTempTestDirectory();
        try
        {
            var ravenConfig = new RavenEmbeddedConfiguration(tempDir);
            var memoryStore = new RavenCampaignMemoryStore(ravenConfig);
            var engine = new LocalWhisperTranscriptionEngine(customWorkerFunc: (_, _, _, _) => Task.FromResult<IReadOnlyList<TranscriptUtteranceDto>>(
            [
                new("utt-amb", 1000, 3000, "Tal vez deberíamos traicionar a la guardia.", 0.80)
            ]));
            var classifier = new TwoStageContextClassifier();
            var queue = new PersistentTranscriptionQueue(engine, classifier, memoryStore);

            const string campaignId = "camp-attention-test";
            const string sessionId = "sess-01";
            const string segmentId = "seg-001";

            queue.EnqueueSegment(new TranscriptionJobRequest(
                CampaignId: campaignId,
                SessionId: sessionId,
                SegmentId: segmentId,
                AudioFilePath: "fake.wav",
                Language: PalantyrLanguage.Spanish));

            var result = await queue.ProcessNextJobAsync();
            Assert.NotNull(result);
            Assert.True(result.Success);

            // Verify NeedsAttention item was created for the ambiguous span (US-05.3, FR-CTX-007)
            var attentionItems = await memoryStore.ListNeedsAttentionItemsAsync(campaignId, sessionId);
            Assert.Single(attentionItems);
            var item = attentionItems[0];
            Assert.Equal("ContextAmbiguity", item.SourceType);
            Assert.False(item.IsResolved);
            Assert.Contains("Eonix pregunta", item.PromptMessageEs);

            // Resolve item
            await memoryStore.ResolveNeedsAttentionItemAsync(campaignId, item.Id, "DM ruled: Was table talk, ignore");
            var resolvedItems = await memoryStore.ListNeedsAttentionItemsAsync(campaignId, sessionId, unresolvedOnly: true);
            Assert.Empty(resolvedItems);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task US_04_1_AC_REL_001_PersistentTranscriptionQueue_Survives_Restart_And_Recovers_Pending_Jobs()
    {
        string tempDir = CreateTempTestDirectory();
        string queueDir = Path.Combine(tempDir, "QueueStorage");
        try
        {
            var ravenConfig = new RavenEmbeddedConfiguration(tempDir);
            var memoryStore = new RavenCampaignMemoryStore(ravenConfig);
            var engine = new LocalWhisperTranscriptionEngine();
            var classifier = new TwoStageContextClassifier();

            const string campaignId = "camp-restart-test";
            const string sessionId = "sess-01";

            // Instance 1: Enqueue two jobs and process only one
            var queue1 = new PersistentTranscriptionQueue(engine, classifier, memoryStore, queueStorageDirectory: queueDir);
            queue1.EnqueueSegment(new TranscriptionJobRequest(
                CampaignId: campaignId,
                SessionId: sessionId,
                SegmentId: "seg-001",
                AudioFilePath: "fake1.wav",
                Language: PalantyrLanguage.Spanish));
            queue1.EnqueueSegment(new TranscriptionJobRequest(
                CampaignId: campaignId,
                SessionId: sessionId,
                SegmentId: "seg-002",
                AudioFilePath: "fake2.wav",
                Language: PalantyrLanguage.Spanish));

            Assert.Equal(2, queue1.PendingCount);

            var firstResult = await queue1.ProcessNextJobAsync();
            Assert.NotNull(firstResult);
            Assert.True(firstResult.Success);
            Assert.Equal(1, queue1.PendingCount);

            // Simulate process termination / restart: Create Instance 2 pointing to same queue storage
            var queue2 = new PersistentTranscriptionQueue(engine, classifier, memoryStore, queueStorageDirectory: queueDir);

            // Durability check: uncompleted seg-002 recovered from disk (AC-REL-001)
            Assert.Equal(1, queue2.PendingCount);
            var secondResult = await queue2.ProcessNextJobAsync();
            Assert.NotNull(secondResult);
            Assert.True(secondResult.Success);
            Assert.Equal("seg-002", secondResult.SegmentId);
            Assert.Equal(0, queue2.PendingCount);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task US_04_5_ProcessPauseCatchUp_Respects_Never_Ask_Always_Policies()
    {
        string tempDir = CreateTempTestDirectory();
        try
        {
            var ravenConfig = new RavenEmbeddedConfiguration(tempDir);
            var memoryStore = new RavenCampaignMemoryStore(ravenConfig);
            var engine = new LocalWhisperTranscriptionEngine();
            var classifier = new TwoStageContextClassifier();
            var queue = new PersistentTranscriptionQueue(engine, classifier, memoryStore, queueStorageDirectory: Path.Combine(tempDir, "Queue"));

            const string campaignId = "camp-pause-test";
            const string sessionId = "sess-pause";

            queue.EnqueueSegment(new TranscriptionJobRequest(campaignId, sessionId, "seg-p1", "f1.wav", PalantyrLanguage.English));
            queue.EnqueueSegment(new TranscriptionJobRequest(campaignId, sessionId, "seg-p2", "f2.wav", PalantyrLanguage.English));

            // 1. Never policy: no jobs processed during pause (FR-PROC-003)
            int neverProcessed = await queue.ProcessPauseCatchUpAsync(campaignId, sessionId, PauseTranscriptionBehavior.Never);
            Assert.Equal(0, neverProcessed);
            Assert.Equal(2, queue.PendingCount);

            // 2. Always policy: catches up and drains backlog
            int alwaysProcessed = await queue.ProcessPauseCatchUpAsync(campaignId, sessionId, PauseTranscriptionBehavior.Always);
            Assert.Equal(2, alwaysProcessed);
            Assert.Equal(0, queue.PendingCount);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    private static string CreateTempTestDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"PalantyrWave2Tests_{Guid.NewGuid():N}");
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
            // Ignore lock contention in temp folder
        }
    }
}
