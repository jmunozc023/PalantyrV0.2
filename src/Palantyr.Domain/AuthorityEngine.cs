using System.Globalization;
using System.Text;
using Palantyr.Contracts;

namespace Palantyr.Domain;

/// <summary>
/// Universal Authority Engine enforcing strict no-auto-merge, deterministic entity resolution,
/// spatiotemporal decoupling, and memory conflict tracking (ADR-004, US-06.1..US-06.6).
/// </summary>
public sealed class CampaignMemoryService
{
    public const double CandidateThreshold = 0.70;

    /// <summary>
    /// Evaluates an observed surface name against existing campaign entities.
    /// Strict Architectural Invariant: AutoMergeThreshold is NEVER. High-similarity matches
    /// are queued for explicit DM ruling in the "Resolve ASAP" queue (US-06.5, FR-MEM-ENTITY-001).
    /// </summary>
    public EntityResolutionCandidateDto EvaluateCandidate(
        string campaignId,
        string observedSurfaceName,
        string? contextSnippet,
        IReadOnlyList<MemoryEntityDocument> existingEntities,
        string? sessionId = null,
        string? segmentId = null,
        string? spanId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentException.ThrowIfNullOrWhiteSpace(observedSurfaceName);

        var candidateId = $"res_{campaignId}_{Guid.NewGuid():N}";
        var cleanSurface = observedSurfaceName.Trim();
        var potentialMatches = new List<CandidateMatchDto>();

        foreach (var entity in existingEntities)
        {
            // 1. Exact match on in-world Canonical Name or Aliases
            if (string.Equals(entity.CanonicalName, cleanSurface, StringComparison.OrdinalIgnoreCase) ||
                entity.Aliases.Any(a => string.Equals(a, cleanSurface, StringComparison.OrdinalIgnoreCase)))
            {
                potentialMatches.Add(new CandidateMatchDto(
                    EntityId: entity.Id,
                    CanonicalName: entity.CanonicalName,
                    EntityType: entity.EntityType,
                    MatchScore: 1.0,
                    MatchReason: ResolutionMatchReason.ExactAliasMatch));
                continue;
            }

            // 2. Exact match on known Speech-to-Text Recognition Variants (FR-MEM-CHAR-002)
            if (entity.RecognitionVariants.Any(v => string.Equals(v, cleanSurface, StringComparison.OrdinalIgnoreCase)))
            {
                potentialMatches.Add(new CandidateMatchDto(
                    EntityId: entity.Id,
                    CanonicalName: entity.CanonicalName,
                    EntityType: entity.EntityType,
                    MatchScore: 0.98,
                    MatchReason: ResolutionMatchReason.KnownSpeechVariant));
                continue;
            }

            // 3. Phonetic and Levenshtein distance matching
            double simCanonical = CalculateSimilarity(cleanSurface, entity.CanonicalName);
            double maxAliasSim = entity.Aliases.Count > 0
                ? entity.Aliases.Max(a => CalculateSimilarity(cleanSurface, a))
                : 0.0;

            double bestSim = Math.Max(simCanonical, maxAliasSim);
            if (bestSim >= CandidateThreshold)
            {
                potentialMatches.Add(new CandidateMatchDto(
                    EntityId: entity.Id,
                    CanonicalName: entity.CanonicalName,
                    EntityType: entity.EntityType,
                    MatchScore: Math.Round(bestSim, 3),
                    MatchReason: ResolutionMatchReason.LevenshteinDistance));
            }
        }

        return new EntityResolutionCandidateDto(
            CandidateId: candidateId,
            CampaignId: campaignId,
            ObservedSurfaceName: cleanSurface,
            ContextSnippet: contextSnippet,
            SourceSessionId: sessionId,
            SourceSegmentId: segmentId,
            SourceSpanId: spanId,
            DetectedAtUtc: DateTimeOffset.UtcNow,
            Status: EntityResolutionStatus.PendingDmReview,
            PotentialMatches: potentialMatches.OrderByDescending(m => m.MatchScore).ToList());
    }

    /// <summary>
    /// Applies a DM resolution ruling to an entity candidate (US-06.5, FR-VAL-001).
    /// Supports linking to existing entity with retroactive repair or registering acoustic recognition variant.
    /// </summary>
    public EntityResolutionCandidateDto ApplyResolutionRuling(
        EntityResolutionCandidateDto candidate,
        ResolveCandidateRequest request,
        Func<string, MemoryEntityDocument?> entityResolver)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(request);

        switch (request.Action.ToLowerInvariant())
        {
            case "linktoexisting":
            case "link":
                if (string.IsNullOrWhiteSpace(request.TargetEntityId))
                {
                    throw new ArgumentException("TargetEntityId required when linking candidate to existing entity.", nameof(request));
                }
                var target = entityResolver(request.TargetEntityId)
                    ?? throw new KeyNotFoundException($"Target entity '{request.TargetEntityId}' not found.");

                if (request.RegisterAsSpeechVariant)
                {
                    if (!target.RecognitionVariants.Contains(candidate.ObservedSurfaceName, StringComparer.OrdinalIgnoreCase))
                    {
                        target.RecognitionVariants.Add(candidate.ObservedSurfaceName);
                    }
                }
                else
                {
                    if (!target.Aliases.Contains(candidate.ObservedSurfaceName, StringComparer.OrdinalIgnoreCase))
                    {
                        target.Aliases.Add(candidate.ObservedSurfaceName);
                    }
                }

                // Attach retroactive provenance ref (AC-ENT-002)
                target.Sources.Add(new ProvenanceRef(
                    SourceType: ProvenanceSourceType.DMCorrection,
                    SessionId: candidate.SourceSessionId,
                    SegmentId: candidate.SourceSegmentId,
                    SpanId: candidate.SourceSpanId,
                    StartTimestampMs: null,
                    EndTimestampMs: null,
                    RecordedAtUtc: DateTimeOffset.UtcNow,
                    NoteOrContext: $"Linked alias '{candidate.ObservedSurfaceName}' via DM ruling by {request.DecidedBy}"));

                return candidate with
                {
                    Status = EntityResolutionStatus.LinkedToExisting,
                    ResolvedEntityId = target.Id,
                    ResolutionDecision = $"Linked to '{target.CanonicalName}' ({target.Id}) by {request.DecidedBy}"
                };

            case "createnew":
            case "create":
                var newName = string.IsNullOrWhiteSpace(request.NewEntityCanonicalName)
                    ? candidate.ObservedSurfaceName
                    : request.NewEntityCanonicalName.Trim();

                return candidate with
                {
                    Status = EntityResolutionStatus.CreatedNewEntity,
                    ResolvedEntityId = null,
                    ResolutionDecision = $"Created new entity '{newName}' by {request.DecidedBy}"
                };

            case "dismiss":
                return candidate with
                {
                    Status = EntityResolutionStatus.Dismissed,
                    ResolutionDecision = $"Dismissed by {request.DecidedBy}: {request.DmNotes ?? "Not an entity"}"
                };

            default:
                throw new ArgumentException($"Unsupported resolution action '{request.Action}'.", nameof(request));
        }
    }

    /// <summary>
    /// Distinguishes state changes over time from true factual contradictions (US-06.6, FR-MEM-CONFLICT-001..007).
    /// True contradictions surface as unresolved conflicts without deleting competing evidence.
    /// </summary>
    public MemoryConflictDto? DetectAssertionConflict(
        string campaignId,
        string entityId,
        string newAssertionText,
        KnowledgeState newKnowledgeState,
        string? newSessionId,
        IReadOnlyList<MemoryHistoryItem> existingHistory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(newAssertionText);

        var cleanNewText = newAssertionText.Trim();

        foreach (var prior in existingHistory)
        {
            if (string.Equals(prior.Description, cleanNewText, StringComparison.OrdinalIgnoreCase))
            {
                continue; // Identical assertion, no conflict
            }

            // Check if prior item was explicitly superseded by a subsequent event (Temporal Evolution)
            if (!string.IsNullOrWhiteSpace(prior.SupersededByHistoryItemId) ||
                !string.IsNullOrWhiteSpace(prior.ValidToSessionId))
            {
                // Temporal evolution, not a contradiction
                continue;
            }

            // Check for epistemic divergence (e.g. Party Belief vs DM Private Truth)
            bool isEpistemicDivergence =
                (prior.KnowledgeState == KnowledgeState.PartyBelief && newKnowledgeState == KnowledgeState.DMPrivateTruth) ||
                (prior.KnowledgeState == KnowledgeState.DMPrivateTruth && newKnowledgeState == KnowledgeState.PartyBelief);

            var conflictType = isEpistemicDivergence
                ? ConflictType.DivergentBelief
                : ConflictType.FactualContradiction;

            return new MemoryConflictDto(
                ConflictId: $"conflict_{campaignId}_{Guid.NewGuid():N}",
                CampaignId: campaignId,
                EntityId: entityId,
                ConflictType: conflictType,
                Summary: $"Conflict between prior '{prior.Description}' ({prior.KnowledgeState}) and new '{cleanNewText}' ({newKnowledgeState})",
                CompetingAssertions:
                [
                    new CompetingAssertionDto(
                        AssertionId: prior.HistoryItemId,
                        AssertionText: prior.Description,
                        SourceRef: null,
                        KnowledgeState: prior.KnowledgeState,
                        Confidence: prior.Confidence,
                        RecordedAtUtc: prior.RecordedAtUtc),
                    new CompetingAssertionDto(
                        AssertionId: $"assert_{Guid.NewGuid():N}",
                        AssertionText: cleanNewText,
                        SourceRef: newSessionId is not null ? new ProvenanceRef(ProvenanceSourceType.Transcript, newSessionId, null, null, null, null, DateTimeOffset.UtcNow) : null,
                        KnowledgeState: newKnowledgeState,
                        Confidence: ConfidenceLevel.MediumConfidence,
                        RecordedAtUtc: DateTimeOffset.UtcNow)
                ],
                State: ConflictResolutionState.Unresolved);
        }

        return null;
    }

    /// <summary>
    /// Normalized Levenshtein distance ratio (1.0 = identical, 0.0 = completely dissimilar).
    /// </summary>
    public static double CalculateSimilarity(string s1, string s2)
    {
        if (string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase))
        {
            return 1.0;
        }

        string norm1 = NormalizeString(s1);
        string norm2 = NormalizeString(s2);

        if (string.Equals(norm1, norm2, StringComparison.OrdinalIgnoreCase))
        {
            return 0.99;
        }

        int maxLength = Math.Max(norm1.Length, norm2.Length);
        if (maxLength == 0)
        {
            return 1.0;
        }

        int distance = ComputeLevenshteinDistance(norm1, norm2);
        return 1.0 - ((double)distance / maxLength);
    }

    private static int ComputeLevenshteinDistance(string s, string t)
    {
        int n = s.Length;
        int m = t.Length;
        var d = new int[n + 1, m + 1];

        if (n == 0) return m;
        if (m == 0) return n;

        for (int i = 0; i <= n; d[i, 0] = i++) { }
        for (int j = 0; j <= m; d[0, j] = j++) { }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (char.ToLowerInvariant(t[j - 1]) == char.ToLowerInvariant(s[i - 1])) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }

    private static string NormalizeString(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (char c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark && !char.IsPunctuation(c))
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC).Trim();
    }
}
