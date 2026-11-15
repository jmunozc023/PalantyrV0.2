using Palantyr.Contracts;

namespace Palantyr.Domain;

/// <summary>
/// Immutable raw machine utterance from transcription engine (ADR-005, US-04.4).
/// </summary>
public sealed record MachineUtterance(
    string UtteranceId,
    long StartMs,
    long EndMs,
    string OriginalText,
    double ConfidenceScore);

/// <summary>
/// Mutable working transcript span with context classification and correction tracking (ADR-005, US-04.4, US-05.1).
/// </summary>
public sealed class WorkingTranscriptSpan
{
    public required string SpanId { get; init; }
    public required string SegmentId { get; init; }
    public required long StartMs { get; init; }
    public required long EndMs { get; init; }
    public required string OriginalText { get; init; }
    public required string CorrectedText { get; set; }
    public ConversationContextType ContextType { get; set; } = ConversationContextType.InWorldCanonCandidate;
    public double ContextConfidence { get; set; } = 0.85;
    public bool IsUserEdited { get; set; }
    public bool IsContextConfirmed { get; set; }
    public string? PromotionReason { get; set; }
    public string? EditedBy { get; set; }
    public DateTimeOffset? LastEditedAtUtc { get; set; }
    public ProcessingLineage? ClassificationLineage { get; set; }

    public WorkingTranscriptSpanDto ToDto() => new(
        SpanId: SpanId,
        SegmentId: SegmentId,
        StartMs: StartMs,
        EndMs: EndMs,
        CorrectedText: CorrectedText,
        OriginalText: OriginalText,
        ContextType: ContextType,
        ContextConfidence: ContextConfidence,
        IsManuallyConfirmed: IsContextConfirmed,
        PromotionReason: PromotionReason,
        ConfirmedBy: EditedBy,
        Lineage: ClassificationLineage);
}

/// <summary>
/// Dual-layer transcript segment persisted per campaign in RavenDB (ADR-005, US-04.4).
/// Layer 1: Immutable MachineUtterances.
/// Layer 2: Mutable WorkingSpans with context classification & DM corrections.
/// </summary>
public sealed class TranscriptSegmentDocument
{
    public required string Id { get; init; } // e.g., "transcripts/camp-01/sess-01/seg-001"
    public required string CampaignId { get; init; }
    public required string SessionId { get; init; }
    public required string SegmentId { get; init; }
    public required int SequenceNumber { get; init; }
    public PalantyrLanguage Language { get; set; } = PalantyrLanguage.Spanish;
    public DateTimeOffset TranscribedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// Layer 1: Raw immutable speech-to-text machine output (FR-TRN-007, US-04.4).
    /// </summary>
    public List<MachineUtterance> MachineUtterances { get; init; } = [];

    /// <summary>
    /// Layer 2: User-facing corrected transcript spans with context classification (FR-CTX-001, FR-COR-001).
    /// </summary>
    public List<WorkingTranscriptSpan> WorkingSpans { get; init; } = [];

    public ProcessingLineage? TranscriptionLineage { get; set; }

    public void ApplyCorrection(string spanId, string newCorrectedText, string correctedBy = "DM")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spanId);
        ArgumentNullException.ThrowIfNull(newCorrectedText);

        var span = WorkingSpans.FirstOrDefault(s => s.SpanId == spanId);
        if (span is not null)
        {
            span.CorrectedText = newCorrectedText;
            span.IsUserEdited = true;
            span.LastEditedAtUtc = DateTimeOffset.UtcNow;
            span.EditedBy = correctedBy;
        }
    }

    public void PromoteJokeToCanon(string spanId, string promotionReason, string confirmedBy = "DM")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spanId);

        var span = WorkingSpans.FirstOrDefault(s => s.SpanId == spanId);
        if (span is not null)
        {
            span.ContextType = ConversationContextType.InWorldCanonCandidate;
            span.IsContextConfirmed = true;
            span.PromotionReason = promotionReason;
            span.EditedBy = confirmedBy;
            span.LastEditedAtUtc = DateTimeOffset.UtcNow;
        }
    }
}

/// <summary>
/// Continuity-critical item routed to Needs Attention (EPIC-05, EPIC-09, US-05.3, US-09.1).
/// </summary>
public sealed class NeedsAttentionItem
{
    public required string Id { get; init; } // e.g., "needs-attention/camp-01/sess-01/item-001"
    public required string CampaignId { get; init; }
    public required string SessionId { get; init; }
    public required string SourceType { get; init; } // e.g., "ContextAmbiguity", "UnconfirmedName"
    public required string Title { get; init; }
    public required string PromptMessageEn { get; init; }
    public required string PromptMessageEs { get; init; }
    public required string TargetSpanId { get; init; }
    public long ApproximateTimestampMs { get; init; }
    public bool IsResolved { get; set; }
    public string? ResolutionDecision { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAtUtc { get; set; }
}
