using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Palantyr.Contracts;
using Palantyr.Domain;

namespace Palantyr.Host;

/// <summary>
/// Redacted diagnostic entry that preserves document IDs, span SHA-256 hashes, timings, and error causes
/// while excluding transcript text, summary prose, and DM-Private content (NFR-PRIV-006, AC-PRIV-002, NFR-REL-005).
/// </summary>
public sealed record RedactedDiagnosticPayload(
    string DocumentOrSegmentId,
    string Category,
    VisibilityScope? Visibility,
    IReadOnlyList<string> SpanHashesSha256,
    string RedactedPreview,
    string? ErrorCode,
    string? ErrorCause,
    DateTimeOffset LoggedAtUtc);

/// <summary>
/// Enforces diagnostic data minimization (NFR-PRIV-006, AC-PRIV-002) and actionable failure cause preservation (NFR-REL-005).
/// Full session transcripts, summary prose, and DM-Only / DM-Private secrets are never emitted into standard logs.
/// </summary>
public static partial class DiagnosticRedactor
{
    public const string RedactionMarker = "[REDACTED:NFR-PRIV-006]";

    [GeneratedRegex(@"(?i)(""(?:originalText|correctedText|summaryProse|dmPrivateNotes|summaryDescription|text|content)""\s*:\s*"")([^""]*)("")")]
    private static partial Regex SensitiveJsonFieldRegex();

    /// <summary>
    /// Redacts a <see cref="MemoryEntityDocument"/> for structured diagnostic logging, stripping
    /// <see cref="MemoryEntityDocument.DmPrivateNotes"/>, <see cref="MemoryEntityDocument.SummaryDescription"/>,
    /// <see cref="MemoryEntityDocument.RecognitionVariants"/>, and embedded <see cref="MemoryHistoryItem"/> prose,
    /// while preserving the document ID, visibility classification, content hash, and error cause.
    /// </summary>
    public static RedactedDiagnosticPayload RedactMemoryEntity(
        MemoryEntityDocument entity,
        string? errorCode = null,
        string? errorCause = null)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var hashes = new List<string>
        {
            ComputeSpanHash(entity.SummaryDescription)
        };

        if (!string.IsNullOrWhiteSpace(entity.DmPrivateNotes))
        {
            hashes.Add(ComputeSpanHash(entity.DmPrivateNotes));
        }

        foreach (var item in entity.StateAndBeliefHistory)
        {
            hashes.Add($"{item.HistoryItemId}:{ComputeSpanHash(item.Description)}");
        }

        return new RedactedDiagnosticPayload(
            DocumentOrSegmentId: entity.Id,
            Category: $"MemoryEntity:{entity.EntityType}",
            Visibility: entity.Visibility,
            SpanHashesSha256: hashes,
            RedactedPreview: $"Id={entity.Id}; Visibility={entity.Visibility}; Content={RedactionMarker}",
            ErrorCode: errorCode,
            ErrorCause: errorCause,
            LoggedAtUtc: DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Redacts a <see cref="TranscriptSegmentResultDto"/> so utterance text is never written to logs,
    /// preserving only the segment ID, utterance IDs, and 12-char SHA-256 span hashes.
    /// </summary>
    public static RedactedDiagnosticPayload RedactTranscriptSegment(
        TranscriptSegmentResultDto segment,
        string? errorCode = null,
        string? errorCause = null)
    {
        ArgumentNullException.ThrowIfNull(segment);

        var spanHashes = segment.Utterances
            .Select(u => $"{u.UtteranceId}:{ComputeSpanHash(u.Text)}")
            .ToList();

        return new RedactedDiagnosticPayload(
            DocumentOrSegmentId: segment.SegmentId,
            Category: "TranscriptSegment",
            Visibility: null,
            SpanHashesSha256: spanHashes,
            RedactedPreview: $"SegmentId={segment.SegmentId}; UtteranceCount={segment.Utterances.Count}; TranscriptText={RedactionMarker}",
            ErrorCode: errorCode,
            ErrorCause: errorCause,
            LoggedAtUtc: DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Redacts sensitive JSON fields (`originalText`, `correctedText`, `summaryProse`, `dmPrivateNotes`, `text`)
    /// and any known sensitive phrases from a diagnostic message while keeping document IDs and error causes intact.
    /// </summary>
    public static string SanitizeLogText(
        string rawMessage,
        IEnumerable<string>? explicitSecretsToRedact = null)
    {
        if (string.IsNullOrEmpty(rawMessage))
        {
            return string.Empty;
        }

        string sanitized = SensitiveJsonFieldRegex().Replace(rawMessage, $"$1{RedactionMarker}$3");

        if (explicitSecretsToRedact is not null)
        {
            foreach (string secret in explicitSecretsToRedact)
            {
                if (!string.IsNullOrWhiteSpace(secret))
                {
                    sanitized = sanitized.Replace(secret, RedactionMarker, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        return sanitized;
    }

    /// <summary>
    /// Serializes a <see cref="RedactedDiagnosticPayload"/> to JSON suitable for diagnostic log sinks.
    /// </summary>
    public static string ToDiagnosticJson(RedactedDiagnosticPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return JsonSerializer.Serialize(payload);
    }

    /// <summary>
    /// Computes a deterministic 12-character lowercase hex SHA-256 fingerprint for span/content correlation
    /// without exposing the underlying text.
    /// </summary>
    public static string ComputeSpanHash(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return "000000000000";
        }

        byte[] bytes = Encoding.UTF8.GetBytes(content);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash)[..12];
    }
}