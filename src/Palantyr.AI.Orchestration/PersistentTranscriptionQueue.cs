using System.Collections.Concurrent;
using Palantyr.Contracts;
using Palantyr.Domain;

namespace Palantyr.AI.Orchestration;

/// <summary>
/// Persistent, priority-aware audio transcription queue and background processor (US-04.3, US-04.5, ADR-008).
/// Enforces Single-Active-GPU-Worker Mutex via `IRuntimePriorityScheduler` and coordinates downstream context classification.
/// </summary>
public sealed class PersistentTranscriptionQueue : ITranscriptionQueue
{
    private readonly ConcurrentQueue<TranscriptionJobInternal> _queue = new();
    private readonly ConcurrentDictionary<string, TranscriptionJobInternal> _allJobs = new(StringComparer.OrdinalIgnoreCase);
    private readonly ITranscriptionEngine _transcriptionEngine;
    private readonly IContextClassifier _contextClassifier;
    private readonly ICampaignMemoryStore _memoryStore;
    private readonly IRuntimePriorityScheduler? _scheduler;

    public PersistentTranscriptionQueue(
        ITranscriptionEngine transcriptionEngine,
        IContextClassifier contextClassifier,
        ICampaignMemoryStore memoryStore,
        IRuntimePriorityScheduler? scheduler = null)
    {
        _transcriptionEngine = transcriptionEngine ?? throw new ArgumentNullException(nameof(transcriptionEngine));
        _contextClassifier = contextClassifier ?? throw new ArgumentNullException(nameof(contextClassifier));
        _memoryStore = memoryStore ?? throw new ArgumentNullException(nameof(memoryStore));
        _scheduler = scheduler;
    }

    public int PendingCount => _queue.Count;

    public int ProcessingCount => _allJobs.Values.Count(j => j.State == SubsystemProcessingState.Processing);

    public void EnqueueSegment(TranscriptionJobRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var jobId = $"job_{request.CampaignId}_{request.SessionId}_{request.SegmentId}";
        var job = new TranscriptionJobInternal
        {
            JobId = jobId,
            Request = request,
            State = SubsystemProcessingState.Pending,
            EnqueuedAtUtc = DateTimeOffset.UtcNow,
            RetryCount = 0
        };

        _allJobs[jobId] = job;
        _queue.Enqueue(job);
    }

    public async Task<TranscriptionJobResult?> ProcessNextJobAsync(CancellationToken cancellationToken = default)
    {
        if (!_queue.TryDequeue(out var job))
        {
            return null;
        }

        // Check if background processing is currently throttled by the scheduler
        if (_scheduler?.IsBackgroundThrottled == true)
        {
            // Re-enqueue for later execution to prevent gaming / VTT frame drops
            _queue.Enqueue(job);
            return null;
        }

        job.State = SubsystemProcessingState.Processing;

        IDisposable? gpuSlot = null;
        if (_scheduler is not null)
        {
            gpuSlot = await _scheduler.AcquireGpuWorkerSlotAsync(
                RuntimePriorityTier.DeferrableBackgroundProcessing,
                cancellationToken).ConfigureAwait(false);
        }

        try
        {
            // 1. Execute Speech-to-Text inference via local Whisper engine
            var transcriptResult = await _transcriptionEngine.TranscribeSegmentAsync(
                job.Request.AudioFilePath,
                job.Request.Language,
                job.Request.FantasyGlossary,
                cancellationToken).ConfigureAwait(false);

            // 2. Classify conversation context for each utterance (EPIC-05, ADR-005)
            var classifiedSpans = await _contextClassifier.ClassifySpansAsync(
                job.Request.CampaignId,
                job.Request.SessionId,
                job.Request.SegmentId,
                transcriptResult.Utterances,
                job.Request.FantasyGlossary,
                cancellationToken).ConfigureAwait(false);

            // 3. Assemble Dual-Layer TranscriptSegmentDocument (ADR-005, US-04.4)
            var machineUtterances = transcriptResult.Utterances
                .Select(u => new MachineUtterance(u.UtteranceId, u.StartMs, u.EndMs, u.Text, u.ConfidenceScore))
                .ToList();

            var workingSpans = classifiedSpans
                .Select(s => new WorkingTranscriptSpan
                {
                    SpanId = s.SpanId,
                    SegmentId = s.SegmentId,
                    StartMs = s.StartMs,
                    EndMs = s.EndMs,
                    OriginalText = s.OriginalText,
                    CorrectedText = s.CorrectedText,
                    ContextType = s.ContextType,
                    ContextConfidence = s.ContextConfidence,
                    IsContextConfirmed = s.IsManuallyConfirmed,
                    PromotionReason = s.PromotionReason,
                    ClassificationLineage = s.Lineage
                })
                .ToList();

            var transcriptDoc = new TranscriptSegmentDocument
            {
                Id = $"transcripts/{job.Request.CampaignId}/{job.Request.SessionId}/{job.Request.SegmentId}",
                CampaignId = job.Request.CampaignId,
                SessionId = job.Request.SessionId,
                SegmentId = job.Request.SegmentId,
                SequenceNumber = ExtractSequenceNumber(job.Request.SegmentId),
                Language = job.Request.Language,
                TranscribedAtUtc = DateTimeOffset.UtcNow,
                MachineUtterances = machineUtterances,
                WorkingSpans = workingSpans,
                TranscriptionLineage = transcriptResult.Lineage
            };

            await _memoryStore.SaveTranscriptSegmentAsync(
                job.Request.CampaignId,
                transcriptDoc,
                cancellationToken).ConfigureAwait(false);

            // 4. Route any Ambiguous spans to Needs Attention (US-05.3, FR-CTX-007)
            foreach (var span in workingSpans.Where(s => s.ContextType == ConversationContextType.Ambiguous))
            {
                var attentionItem = new NeedsAttentionItem
                {
                    Id = $"needs-attention/{job.Request.CampaignId}/{job.Request.SessionId}/{span.SpanId}",
                    CampaignId = job.Request.CampaignId,
                    SessionId = job.Request.SessionId,
                    SourceType = "ContextAmbiguity",
                    Title = job.Request.Language == PalantyrLanguage.Spanish
                        ? "Contexto ambiguo: ¿Es canon o charla fuera de rol?"
                        : "Ambiguous context: Canon or table talk?",
                    PromptMessageEn = $"Eonix asks: In segment '{job.Request.SegmentId}', was '{span.OriginalText}' an in-world action or out-of-character banter?",
                    PromptMessageEs = $"Eonix pregunta: En el segmento '{job.Request.SegmentId}', ¿'{span.OriginalText}' fue una acción del juego o broma fuera de personaje?",
                    TargetSpanId = span.SpanId,
                    ApproximateTimestampMs = span.StartMs,
                    IsResolved = false
                };

                await _memoryStore.SaveNeedsAttentionItemAsync(
                    job.Request.CampaignId,
                    attentionItem,
                    cancellationToken).ConfigureAwait(false);
            }

            job.State = SubsystemProcessingState.Completed;
            job.CompletedAtUtc = DateTimeOffset.UtcNow;

            return new TranscriptionJobResult(
                CampaignId: job.Request.CampaignId,
                SessionId: job.Request.SessionId,
                SegmentId: job.Request.SegmentId,
                Success: true,
                Transcript: transcriptResult,
                ErrorMessage: null);
        }
        catch (Exception ex)
        {
            job.RetryCount++;
            if (job.RetryCount < 3)
            {
                job.State = SubsystemProcessingState.Pending;
                _queue.Enqueue(job); // Exponential backoff retry
            }
            else
            {
                job.State = SubsystemProcessingState.Failed;
                job.ErrorMessage = ex.Message;
            }

            return new TranscriptionJobResult(
                CampaignId: job.Request.CampaignId,
                SessionId: job.Request.SessionId,
                SegmentId: job.Request.SegmentId,
                Success: false,
                Transcript: null,
                ErrorMessage: ex.Message);
        }
        finally
        {
            gpuSlot?.Dispose();
        }
    }

    public IReadOnlyList<TranscriptionJobStatusDto> GetQueueStatus(string campaignId, string? sessionId = null)
    {
        var query = _allJobs.Values.Where(j => string.Equals(j.Request.CampaignId, campaignId, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            query = query.Where(j => string.Equals(j.Request.SessionId, sessionId, StringComparison.OrdinalIgnoreCase));
        }

        return query
            .OrderBy(j => j.EnqueuedAtUtc)
            .Select(j => new TranscriptionJobStatusDto(
                JobId: j.JobId,
                CampaignId: j.Request.CampaignId,
                SessionId: j.Request.SessionId,
                SegmentId: j.Request.SegmentId,
                State: j.State,
                EnqueuedAtUtc: j.EnqueuedAtUtc,
                CompletedAtUtc: j.CompletedAtUtc,
                RetryCount: j.RetryCount,
                ErrorMessage: j.ErrorMessage))
            .ToList();
    }

    private static int ExtractSequenceNumber(string segmentId)
    {
        // e.g. "seg-001" or "segment_002"
        var digits = new string(segmentId.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var seq) ? seq : 1;
    }

    private sealed class TranscriptionJobInternal
    {
        public required string JobId { get; init; }
        public required TranscriptionJobRequest Request { get; init; }
        public required SubsystemProcessingState State { get; set; }
        public required DateTimeOffset EnqueuedAtUtc { get; init; }
        public DateTimeOffset? CompletedAtUtc { get; set; }
        public int RetryCount { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
