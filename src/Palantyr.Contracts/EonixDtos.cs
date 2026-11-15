using System;
using System.Collections.Generic;

namespace Palantyr.Contracts;

/// <summary>
/// Knowledge scope boundary for Eonix queries (Sec 14, US-10.2).
/// Prevents accidental DM secret leakage during play.
/// </summary>
public enum EonixScope
{
    CampaignDmFull = 0,
    PartyKnowledge = 1,
    CharacterScoped = 2
}

/// <summary>
/// Detailed evidence citation backing an Eonix statement (ADR-007, US-10.5, AC-EONIX-003).
/// </summary>
public sealed record EvidenceSourceDto(
    string SourceId,
    ProvenanceSourceType SourceType,
    string? EntityId,
    string? SessionId,
    string? SegmentId,
    string? SpanId,
    long? TimestampMs,
    string Snippet,
    KnowledgeState KnowledgeState,
    ConfidenceLevel Confidence);

/// <summary>
/// Natural language text query payload sent to Eonix (US-10.1, US-10.2).
/// </summary>
public sealed record EonixChatQueryRequest(
    string Query,
    EonixScope Scope = EonixScope.CampaignDmFull,
    string? ScopeCharacterId = null,
    PalantyrLanguage Language = PalantyrLanguage.English);

/// <summary>
/// Grounded answer from Eonix citing explicit evidence or honestly admitting unknown (US-10.2, US-10.3, US-10.5, AC-EONIX-001).
/// </summary>
public sealed record EonixChatQueryResponse(
    string Answer,
    bool IsGrounded,
    ConfidenceLevel Confidence,
    double ConfidenceScore,
    IReadOnlyList<EvidenceSourceDto> Sources,
    EonixScope ScopeApplied);

/// <summary>
/// Natural language teaching request to directly impart facts or corrections to Eonix (US-10.4, FR-MEM-DM-001..007).
/// </summary>
public sealed record EonixTeachRequest(
    string TaughtStatement,
    string? TargetEntityName = null,
    bool IsDmSecret = false,
    string? ClarificationResponse = null,
    string TaughtBy = "DM");

/// <summary>
/// Outcome of conversational teaching (US-10.4, AC-EONIX-002).
/// </summary>
public sealed record EonixTeachResponse(
    bool Success,
    string TargetEntityId,
    string TargetCanonicalName,
    KnowledgeState KnowledgeState,
    VisibilityScope Visibility,
    bool RequiresClarification,
    string? ClarificationQuestion);
