using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Palantyr.Contracts;

namespace Palantyr.Domain;

/// <summary>
/// Domain service implementing Talk to Eonix: Hybrid Grounded RAG, Knowledge-Scope filtering,
/// Conversational Teaching, and Evidence Sourcing (ADR-007, EPIC-10, US-10.1..10.5).
/// </summary>
public sealed class EonixAssistantService : IEonixAssistantService
{
    private readonly ICampaignMemoryStore _memoryStore;
    private readonly IRuntimePriorityScheduler? _priorityScheduler;

    public EonixAssistantService(
        ICampaignMemoryStore memoryStore,
        IRuntimePriorityScheduler? priorityScheduler = null)
    {
        _memoryStore = memoryStore ?? throw new ArgumentNullException(nameof(memoryStore));
        _priorityScheduler = priorityScheduler;
    }

    /// <summary>
    /// Answers a text question grounded strictly in campaign memory (ADR-007, US-10.2, AC-EONIX-001).
    /// Single-Active-GPU-Worker Mutex (ADR-008): Protected by RuntimePriorityScheduler.
    /// </summary>
    public async Task<EonixChatQueryResponse> AskAsync(
        string campaignId,
        EonixChatQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentNullException.ThrowIfNull(request);

        IDisposable? gpuLease = null;
        if (_priorityScheduler is not null)
        {
            gpuLease = await _priorityScheduler.AcquireGpuWorkerSlotAsync(
                RuntimePriorityTier.HighInteractiveEonixQuery,
                cancellationToken);
        }

        try
        {
            var cleanQuery = request.Query.Trim();
            var isSpanish = request.Language == PalantyrLanguage.Spanish;

            if (string.IsNullOrWhiteSpace(cleanQuery))
            {
                return new EonixChatQueryResponse(
                    Answer: isSpanish ? "Dime qué deseas consultar, Maestro del Calabozo." : "Tell me what you wish to consult, Dungeon Master.",
                    IsGrounded: false,
                    Confidence: ConfidenceLevel.LowOrUncertain,
                    ConfidenceScore: 0.0,
                    Sources: [],
                    ScopeApplied: request.Scope);
            }

            // 1. Retrieve candidates based on knowledge scope (ADR-006, US-10.2)
            // CharacterScoped and PartyKnowledge MUST be routed through QueryPartySafeMemoryAsync to exclude DM secrets
            IReadOnlyList<PartySafeMemoryView>? partySafeEntities = null;
            IReadOnlyList<MemoryEntityDocument>? dmFullEntities = null;

            var visibilityContext = request.Scope switch
            {
                EonixScope.PartyKnowledge => QueryVisibilityContext.PartySafe,
                EonixScope.CharacterScoped => new QueryVisibilityContext(
                    IncludeDmPrivate: false,
                    ScopedCharacterIds: !string.IsNullOrWhiteSpace(request.ScopeCharacterId) ? [request.ScopeCharacterId] : null),
                _ => null
            };

            if (visibilityContext is not null)
            {
                partySafeEntities = await _memoryStore.QueryPartySafeMemoryAsync(
                    campaignId,
                    visibilityContext,
                    cancellationToken: cancellationToken);
            }
            else
            {
                dmFullEntities = await _memoryStore.QueryDmFullMemoryAsync(campaignId, cancellationToken: cancellationToken);
            }

            // 2. Search candidates (exact name, alias, or significant description tokens)
            var queryTokens = cleanQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "where", "what", "who", "when", "why", "how", "is", "are", "was", "were",
                "the", "a", "an", "in", "on", "at", "to", "for", "of", "with", "by", "about",
                "tell", "me", "dónde", "donde", "qué", "que", "quién", "quien", "cuándo", "cuando",
                "cómo", "como", "el", "la", "los", "las", "un", "una", "unos", "unas", "de", "del", "en"
            };
            var significantTokens = queryTokens
                .Select(t => t.Trim('?', '.', '!', ',', ';', ':', '"', '\'', '¿', '¡'))
                .Where(t => t.Length > 2 && !stopWords.Contains(t))
                .ToList();

        bool IsCandidateMatch(string canonicalName, IReadOnlyList<string> aliases, string description)
        {
            if (string.Equals(canonicalName, cleanQuery, StringComparison.OrdinalIgnoreCase)) return true;
            if (aliases.Any(a => string.Equals(a, cleanQuery, StringComparison.OrdinalIgnoreCase))) return true;
            if (cleanQuery.Contains(canonicalName, StringComparison.OrdinalIgnoreCase)) return true;
            if (aliases.Any(a => cleanQuery.Contains(a, StringComparison.OrdinalIgnoreCase))) return true;
            if (significantTokens.Count > 0 && significantTokens.Any(t => canonicalName.Contains(t, StringComparison.OrdinalIgnoreCase))) return true;
            if (significantTokens.Count >= 2 && significantTokens.All(t => description.Contains(t, StringComparison.OrdinalIgnoreCase))) return true;
            return false;
        }

        var sources = new List<EvidenceSourceDto>();
        string answer;
        ConfidenceLevel confidence = ConfidenceLevel.MediumConfidence;
        double confidenceScore = 0.50;

        if (partySafeEntities is not null)
        {
            var match = partySafeEntities.FirstOrDefault(e => IsCandidateMatch(e.CanonicalName, e.Aliases, e.SummaryDescription));

            if (match is not null)
            {
                confidence = match.Confidence;
                confidenceScore = 0.88;

                var sb = new StringBuilder();
                if (isSpanish)
                {
                    sb.Append($"{match.CanonicalName} ");
                    if (match.Aliases.Count > 0) sb.Append($"(también conocido como {string.Join(", ", match.Aliases)}) ");
                    sb.Append($"es {match.SummaryDescription} ");
                    if (match.LocationFacet is not null)
                    {
                        sb.Append($"Ubicación: {match.LocationFacet.ParentLocationId ?? "Reino"}. ");
                    }
                    if (match.CharacterFacet?.StatementsBy.Count > 0)
                    {
                        sb.Append($"Afirmó: \"{match.CharacterFacet.StatementsBy[0].SummaryText}\". ");
                    }
                }
                else
                {
                    sb.Append($"{match.CanonicalName} ");
                    if (match.Aliases.Count > 0) sb.Append($"(also known as {string.Join(", ", match.Aliases)}) ");
                    sb.Append($"is {match.SummaryDescription} ");
                    if (match.LocationFacet is not null)
                    {
                        sb.Append($"Located in {match.LocationFacet.ParentLocationId ?? "the realm"}. ");
                    }
                    if (match.CharacterFacet?.StatementsBy.Count > 0)
                    {
                        sb.Append($"Stated: \"{match.CharacterFacet.StatementsBy[0].SummaryText}\". ");
                    }
                }

                answer = sb.ToString().Trim();

                sources.Add(new EvidenceSourceDto(
                    SourceId: $"src_{match.Id}",
                    SourceType: ProvenanceSourceType.Transcript,
                    EntityId: match.Id,
                    SessionId: match.FirstAppearance?.SessionId,
                    SegmentId: null,
                    SpanId: null,
                    TimestampMs: match.FirstAppearance?.TimestampMs,
                    Snippet: match.SummaryDescription,
                    KnowledgeState: match.KnowledgeState,
                    Confidence: match.Confidence));

                return new EonixChatQueryResponse(
                    Answer: answer,
                    IsGrounded: true,
                    Confidence: confidence,
                    ConfidenceScore: confidenceScore,
                    Sources: sources,
                    ScopeApplied: request.Scope);
            }
        }
        else if (dmFullEntities is not null)
        {
            var match = dmFullEntities.FirstOrDefault(e => IsCandidateMatch(e.CanonicalName, e.Aliases, e.SummaryDescription));

            if (match is not null)
            {
                confidence = match.Confidence;
                confidenceScore = 0.95;

                var sb = new StringBuilder();
                if (isSpanish)
                {
                    sb.Append($"{match.CanonicalName}: {match.SummaryDescription} ");
                    if (!string.IsNullOrWhiteSpace(match.DmPrivateNotes))
                    {
                        sb.Append($"[SECRETO DM]: {match.DmPrivateNotes} ");
                    }
                }
                else
                {
                    sb.Append($"{match.CanonicalName}: {match.SummaryDescription} ");
                    if (!string.IsNullOrWhiteSpace(match.DmPrivateNotes))
                    {
                        sb.Append($"[DM SECRET]: {match.DmPrivateNotes} ");
                    }
                }

                answer = sb.ToString().Trim();

                sources.Add(new EvidenceSourceDto(
                    SourceId: $"src_{match.Id}",
                    SourceType: ProvenanceSourceType.DMDirectNote,
                    EntityId: match.Id,
                    SessionId: match.FirstAppearance?.SessionId,
                    SegmentId: null,
                    SpanId: null,
                    TimestampMs: match.FirstAppearance?.TimestampMs,
                    Snippet: match.SummaryDescription,
                    KnowledgeState: match.KnowledgeState,
                    Confidence: match.Confidence));

                return new EonixChatQueryResponse(
                    Answer: answer,
                    IsGrounded: true,
                    Confidence: confidence,
                    ConfidenceScore: confidenceScore,
                    Sources: sources,
                    ScopeApplied: request.Scope);
            }
        }

        // 3. Strict Grounding Invariant (FR-EONIX-013): No evidence -> honestly admit unknown
        var honestUnknown = isSpanish
            ? "No tengo registros ni evidencia sobre eso en esta campaña, Maestro."
            : "I have no recorded memories or evidence regarding that in this campaign, Master.";

        return new EonixChatQueryResponse(
            Answer: honestUnknown,
            IsGrounded: false,
            Confidence: ConfidenceLevel.LowOrUncertain,
            ConfidenceScore: 0.10,
            Sources: [],
            ScopeApplied: request.Scope);
        }
        finally
        {
            gpuLease?.Dispose();
        }
    }

    /// <summary>
    /// Imparts direct facts, lore, or secrets to Eonix via natural language (US-10.4, FR-MEM-DM-001..007).
    /// Single-Active-GPU-Worker Mutex (ADR-008): Protected by RuntimePriorityScheduler.
    /// </summary>
    public async Task<EonixTeachResponse> TeachAsync(
        string campaignId,
        EonixTeachRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentNullException.ThrowIfNull(request);

        IDisposable? gpuLease = null;
        if (_priorityScheduler is not null)
        {
            gpuLease = await _priorityScheduler.AcquireGpuWorkerSlotAsync(
                RuntimePriorityTier.HighInteractiveEonixQuery,
                cancellationToken);
        }

        try
        {
            var rawStatement = request.TaughtStatement.Trim();
            var entityName = request.TargetEntityName?.Trim();

            if (string.IsNullOrWhiteSpace(entityName))
            {
                // Multi-lingual copula extraction heuristic for English & Spanish
                string[] copulaMarkers = [" is ", " are ", " was ", " were ", " es ", " son ", " era ", " eran "];
                int splitIdx = -1;
                foreach (var marker in copulaMarkers)
                {
                    int idx = rawStatement.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                    if (idx > 0 && (splitIdx == -1 || idx < splitIdx))
                    {
                        splitIdx = idx;
                    }
                }

                if (splitIdx > 0)
                {
                    entityName = rawStatement[..splitIdx].Trim();
                }
                else
                {
                    entityName = "CampLore_" + Guid.NewGuid().ToString("N")[..8];
                }
            }

        var existingEntities = await _memoryStore.ListMemoryEntitiesAsync(campaignId, cancellationToken: cancellationToken);
        var targetEntity = existingEntities.FirstOrDefault(e =>
            string.Equals(e.CanonicalName, entityName, StringComparison.OrdinalIgnoreCase) ||
            e.Aliases.Any(a => string.Equals(a, entityName, StringComparison.OrdinalIgnoreCase)));

        var targetId = targetEntity?.Id ?? $"lore_{Guid.NewGuid():N}"[..18];
        var visibility = request.IsDmSecret ? VisibilityScope.DMPrivate : VisibilityScope.PartyKnown;
        var knowledge = request.IsDmSecret ? KnowledgeState.DMPrivateTruth : KnowledgeState.CampaignTruth;

        if (targetEntity is not null)
        {
            if (request.IsDmSecret)
            {
                targetEntity.DmPrivateNotes = string.IsNullOrWhiteSpace(targetEntity.DmPrivateNotes)
                    ? rawStatement
                    : $"{targetEntity.DmPrivateNotes}; {rawStatement}";
            }
            else
            {
                targetEntity.SummaryDescription = $"{targetEntity.SummaryDescription} {rawStatement}".Trim();
            }

            targetEntity.Sources.Add(new ProvenanceRef(
                SourceType: ProvenanceSourceType.DMConversationalTeaching,
                SessionId: null,
                SegmentId: null,
                SpanId: null,
                StartTimestampMs: null,
                EndTimestampMs: null,
                RecordedAtUtc: DateTimeOffset.UtcNow,
                NoteOrContext: $"Taught directly by {request.TaughtBy}"));

            await _memoryStore.SaveMemoryEntityAsync(campaignId, targetEntity, cancellationToken);
        }
        else
        {
            var newEntity = new MemoryEntityDocument
            {
                Id = targetId,
                EntityType = MemoryEntityType.Character,
                CanonicalName = entityName,
                SummaryDescription = request.IsDmSecret ? string.Empty : rawStatement,
                DmPrivateNotes = request.IsDmSecret ? rawStatement : null,
                Visibility = visibility,
                KnowledgeState = knowledge,
                Confidence = ConfidenceLevel.DMConfirmed,
                Sources =
                [
                    new ProvenanceRef(
                        SourceType: ProvenanceSourceType.DMConversationalTeaching,
                        SessionId: null,
                        SegmentId: null,
                        SpanId: null,
                        StartTimestampMs: null,
                        EndTimestampMs: null,
                        RecordedAtUtc: DateTimeOffset.UtcNow,
                        NoteOrContext: $"Created via conversational teaching by {request.TaughtBy}")
                ]
            };

            await _memoryStore.SaveMemoryEntityAsync(campaignId, newEntity, cancellationToken);
        }

        return new EonixTeachResponse(
            Success: true,
            TargetEntityId: targetId,
            TargetCanonicalName: entityName,
            KnowledgeState: knowledge,
            Visibility: visibility,
            RequiresClarification: false,
            ClarificationQuestion: null);
        }
        finally
        {
            gpuLease?.Dispose();
        }
    }
}
