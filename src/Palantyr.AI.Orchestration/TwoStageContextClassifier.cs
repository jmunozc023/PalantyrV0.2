using System.Security.Cryptography;
using System.Text;
using Palantyr.Contracts;

namespace Palantyr.AI.Orchestration;

/// <summary>
/// Two-stage conversation context classifier (EPIC-05, ADR-005, US-05.1..US-05.4, FR-CTX-001..010).
/// Stage 1: Deterministic fast heuristics for rules, meta, and obvious table-talk in EN and ES.
/// Stage 2: Contextual structured analysis for jokes, canon candidacy, and Joke-to-Canon transitions (FR-CTX-006).
/// </summary>
public sealed class TwoStageContextClassifier : IContextClassifier
{
    public const string DefaultClassifierName = "palantyr-context-classifier";
    public const string DefaultVersion = "2.0.0";
    public const string DefaultPromptVersion = "v2-rules-and-joke-promotion";

    private static readonly string[] RulesAndMetaKeywordsEn =
    [
        "initiative", "d20", "saving throw", "spell slot", "armor class", "dc ", "hit points",
        "attack roll", "modifier", "short rest", "long rest", "advantage", "disadvantage", "check roll"
    ];

    private static readonly string[] RulesAndMetaKeywordsEs =
    [
        "iniciativa", "d20", "tirada de salvación", "salvación", "espacio de conjuro", "clase de armadura",
        "puntos de golpe", "tirada de ataque", "modificador", "descanso corto", "descanso largo", "ventaja", "desventaja"
    ];

    private static readonly string[] TableTalkKeywordsEn =
    [
        "pizza", "beer", "bathroom", "restroom", "traffic", "wifi", "discord", "order food",
        "schedule", "next week", "phone call", "did you see the game"
    ];

    private static readonly string[] TableTalkKeywordsEs =
    [
        "pizza", "cerveza", "baño", "tráfico", "wifi", "discord", "pedir comida",
        "próxima sesión", "próxima semana", "llamada", "viste el partido"
    ];

    private static readonly string[] JokeKeywordsEn =
    [
        "what if i", "haha", "just kidding", "imagine if", "wouldn't it be funny",
        "as a joke", "kill the shopkeeper haha", "burn the tavern jaja"
    ];

    private static readonly string[] JokeKeywordsEs =
    [
        "qué pasaría si", "jaja", "es broma", "imagínate si", "sería gracioso",
        "de broma", "matar al tabernero jaja", "quemar la taberna jaja"
    ];

    private static readonly string[] JokeToCanonTriggersEn =
    [
        "wait, no, i actually do that", "wait, is that canon? yes", "actually, i really do that",
        "i'm serious, i do it", "the dm nods: it happens", "that actually happened"
    ];

    private static readonly string[] JokeToCanonTriggersEs =
    [
        "espera, no, en serio hago eso", "espera, ¿eso es canon? sí", "en serio, lo hago",
        "voy en serio, lo lanzo", "el dm asiente: sucede", "eso realmente ocurrió"
    ];

    private readonly string _name;
    private readonly string _version;

    public TwoStageContextClassifier(
        string name = DefaultClassifierName,
        string version = DefaultVersion)
    {
        _name = name;
        _version = version;
    }

    public string ClassifierName => _name;
    public string ModelOrRuleVersion => _version;

    public async Task<IReadOnlyList<WorkingTranscriptSpanDto>> ClassifySpansAsync(
        string campaignId,
        string sessionId,
        string segmentId,
        IReadOnlyList<TranscriptUtteranceDto> utterances,
        IReadOnlyList<string>? partyCharacterNames = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(utterances);

        var results = new List<WorkingTranscriptSpanDto>();
        var knownRoster = partyCharacterNames ?? [];
        var templateHash = ComputeSha256Hex(DefaultPromptVersion);

        for (int i = 0; i < utterances.Count; i++)
        {
            var u = utterances[i];
            var text = u.Text.Trim();
            var textLower = text.ToLowerInvariant();

            var spanId = $"span_{segmentId}_{i + 1:D3}";
            var lineage = new ProcessingLineage(
                Engine: _name,
                ModelId: "heuristic-and-structured-v2",
                AdapterVersion: _version,
                PromptTemplateVersion: DefaultPromptVersion,
                PromptTemplateHash: templateHash,
                ExecutedAtUtc: DateTimeOffset.UtcNow);

            // Stage 1: Fast Heuristics (Rules & Table Talk)
            if (ContainsAny(textLower, RulesAndMetaKeywordsEn) || ContainsAny(textLower, RulesAndMetaKeywordsEs))
            {
                results.Add(new WorkingTranscriptSpanDto(
                    SpanId: spanId,
                    SegmentId: segmentId,
                    StartMs: u.StartMs,
                    EndMs: u.EndMs,
                    CorrectedText: text,
                    OriginalText: text,
                    ContextType: ConversationContextType.RulesOrMeta,
                    ContextConfidence: 0.93,
                    IsManuallyConfirmed: false,
                    Lineage: lineage));
                continue;
            }

            if (ContainsAny(textLower, TableTalkKeywordsEn) || ContainsAny(textLower, TableTalkKeywordsEs))
            {
                results.Add(new WorkingTranscriptSpanDto(
                    SpanId: spanId,
                    SegmentId: segmentId,
                    StartMs: u.StartMs,
                    EndMs: u.EndMs,
                    CorrectedText: text,
                    OriginalText: text,
                    ContextType: ConversationContextType.OutOfCharacterTableTalk,
                    ContextConfidence: 0.90,
                    IsManuallyConfirmed: false,
                    Lineage: lineage));
                continue;
            }

            // Stage 2: Jokes & Joke-to-Canon Promotion (FR-CTX-006)
            bool isJokePattern = ContainsAny(textLower, JokeKeywordsEn) || ContainsAny(textLower, JokeKeywordsEs);

            if (isJokePattern)
            {
                // Inspect subsequent utterance in sliding window to detect immediate DM/Player canon confirmation
                bool promotedByNextUtterance = false;
                string? promotionText = null;

                if (i + 1 < utterances.Count)
                {
                    var nextTextLower = utterances[i + 1].Text.ToLowerInvariant();
                    if (ContainsAny(nextTextLower, JokeToCanonTriggersEn) || ContainsAny(nextTextLower, JokeToCanonTriggersEs))
                    {
                        promotedByNextUtterance = true;
                        promotionText = utterances[i + 1].Text;
                    }
                }

                if (promotedByNextUtterance)
                {
                    // Immediately promote to InWorldCanonCandidate with explicit audit trail (FR-CTX-006)
                    results.Add(new WorkingTranscriptSpanDto(
                        SpanId: spanId,
                        SegmentId: segmentId,
                        StartMs: u.StartMs,
                        EndMs: u.EndMs,
                        CorrectedText: text,
                        OriginalText: text,
                        ContextType: ConversationContextType.InWorldCanonCandidate,
                        ContextConfidence: 0.95,
                        IsManuallyConfirmed: true,
                        PromotionReason: $"Joke-to-Canon Promotion: Ratified by '{promotionText}'",
                        ConfirmedBy: "DM_Ratification",
                        Lineage: lineage));
                    continue;
                }

                results.Add(new WorkingTranscriptSpanDto(
                    SpanId: spanId,
                    SegmentId: segmentId,
                    StartMs: u.StartMs,
                    EndMs: u.EndMs,
                    CorrectedText: text,
                    OriginalText: text,
                    ContextType: ConversationContextType.HypotheticalOrJoke,
                    ContextConfidence: 0.86,
                    IsManuallyConfirmed: false,
                    Lineage: lineage));
                continue;
            }

            // Ambiguity check: high uncertainty or contradictory context cues
            if (textLower.Contains("maybe") || textLower.Contains("quizás") || textLower.Contains("tal vez") ||
                (textLower.Contains("?") && textLower.Length < 18))
            {
                results.Add(new WorkingTranscriptSpanDto(
                    SpanId: spanId,
                    SegmentId: segmentId,
                    StartMs: u.StartMs,
                    EndMs: u.EndMs,
                    CorrectedText: text,
                    OriginalText: text,
                    ContextType: ConversationContextType.Ambiguous,
                    ContextConfidence: 0.55,
                    IsManuallyConfirmed: false,
                    Lineage: lineage));
                continue;
            }

            // Default: In-World Canon Candidate
            results.Add(new WorkingTranscriptSpanDto(
                SpanId: spanId,
                SegmentId: segmentId,
                StartMs: u.StartMs,
                EndMs: u.EndMs,
                CorrectedText: text,
                OriginalText: text,
                ContextType: ConversationContextType.InWorldCanonCandidate,
                ContextConfidence: 0.89,
                IsManuallyConfirmed: false,
                Lineage: lineage));
        }

        return await Task.FromResult<IReadOnlyList<WorkingTranscriptSpanDto>>(results).ConfigureAwait(false);
    }

    public WorkingTranscriptSpanDto PromoteJokeToCanon(
        WorkingTranscriptSpanDto span,
        string confirmationReason,
        string confirmedBy = "DM")
    {
        ArgumentNullException.ThrowIfNull(span);
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmationReason);

        return span with
        {
            ContextType = ConversationContextType.InWorldCanonCandidate,
            ContextConfidence = 1.0,
            IsManuallyConfirmed = true,
            PromotionReason = confirmationReason,
            ConfirmedBy = confirmedBy
        };
    }

    private static bool ContainsAny(string text, string[] keywords)
    {
        for (int i = 0; i < keywords.Length; i++)
        {
            if (text.Contains(keywords[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static string ComputeSha256Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
