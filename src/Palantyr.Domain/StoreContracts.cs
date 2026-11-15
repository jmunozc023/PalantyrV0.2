using Palantyr.Contracts;

namespace Palantyr.Domain;

/// <summary>
/// System-level store (`Palantyr_System` DB) for settings, hardware assessment, and campaign catalog.
/// </summary>
public interface ISystemStore
{
    Task<AppSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<AppSettingsDto> SaveSettingsAsync(AppSettingsDto settings, CancellationToken cancellationToken = default);
    Task<SystemAssessmentProfile?> GetLatestAssessmentAsync(CancellationToken cancellationToken = default);
    Task SaveAssessmentAsync(SystemAssessmentProfile profile, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CampaignSummaryDto>> ListCampaignsAsync(CancellationToken cancellationToken = default);
    Task<CampaignDocument> CreateCampaignAsync(CreateCampaignRequest request, CancellationToken cancellationToken = default);
    Task<CampaignDocument?> GetCampaignAsync(string campaignId, CancellationToken cancellationToken = default);
    Task<CampaignDocument> UpdatePartyRosterAsync(string campaignId, IReadOnlyList<PartyMemberDto> roster, CancellationToken cancellationToken = default);
    Task<string?> DeleteCampaignSafelyAsync(string campaignId, CampaignDeletionOption option, string backupDirectory, CancellationToken cancellationToken = default);
}

/// <summary>
/// Per-campaign store (`Palantyr_Campaign_<Id>` DB) for sessions, lore, transcripts, and Universal Memory entities.
/// </summary>
public interface ICampaignMemoryStore
{
    Task UpsertSessionFromWalAsync(string campaignId, SessionDocument session, CancellationToken cancellationToken = default);
    Task<SessionDocument?> GetSessionAsync(string campaignId, string sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SessionDocument>> ListSessionsAsync(string campaignId, CancellationToken cancellationToken = default);
    Task SaveMemoryEntityAsync(string campaignId, MemoryEntityDocument entity, CancellationToken cancellationToken = default);
    Task<MemoryEntityDocument?> GetMemoryEntityAsync(string campaignId, string entityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryEntityDocument>> ListMemoryEntitiesAsync(string campaignId, MemoryEntityType? filterType = null, string? searchQuery = null, CancellationToken cancellationToken = default);
    Task DeleteMemoryEntityAsync(string campaignId, string entityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PartySafeMemoryView>> QueryPartySafeMemoryAsync(string campaignId, QueryVisibilityContext visibilityContext, MemoryEntityType? filterType = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryEntityDocument>> QueryDmFullMemoryAsync(string campaignId, MemoryEntityType? filterType = null, CancellationToken cancellationToken = default);
    Task<string> CreateCampaignBackupAsync(string campaignId, string targetDirectory, bool includeRetainedAudio, CancellationToken cancellationToken = default);
    
    // Wave 2: Dual Transcript and Needs Attention persistence (ADR-005, US-04.4, US-05.3)
    Task SaveTranscriptSegmentAsync(string campaignId, TranscriptSegmentDocument transcript, CancellationToken cancellationToken = default);
    Task<TranscriptSegmentDocument?> GetTranscriptSegmentAsync(string campaignId, string sessionId, string segmentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TranscriptSegmentDocument>> ListTranscriptSegmentsAsync(string campaignId, string sessionId, CancellationToken cancellationToken = default);
    Task SaveNeedsAttentionItemAsync(string campaignId, NeedsAttentionItem item, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NeedsAttentionItem>> ListNeedsAttentionItemsAsync(string campaignId, string? sessionId = null, bool unresolvedOnly = true, CancellationToken cancellationToken = default);
    Task ResolveNeedsAttentionItemAsync(string campaignId, string itemId, string resolutionDecision, CancellationToken cancellationToken = default);

    // Wave 3A: Entity Resolution, Conflict & Spatiotemporal store operations (US-06.1..06.7)
    Task SaveResolutionCandidateAsync(string campaignId, EntityResolutionCandidateDto candidate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EntityResolutionCandidateDto>> ListResolutionCandidatesAsync(string campaignId, EntityResolutionStatus? statusFilter = null, CancellationToken cancellationToken = default);
    Task<EntityResolutionCandidateDto?> GetResolutionCandidateAsync(string campaignId, string candidateId, CancellationToken cancellationToken = default);
    Task SaveConflictAsync(string campaignId, MemoryConflictDto conflict, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryConflictDto>> ListConflictsAsync(string campaignId, string? entityId = null, bool unresolvedOnly = true, CancellationToken cancellationToken = default);
    Task<MemoryConflictDto?> GetConflictAsync(string campaignId, string conflictId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LocationHierarchyNodeDto>> GetLocationHierarchyTreeAsync(string campaignId, CancellationToken cancellationToken = default);

    // Wave 4: Session Summaries, Review & Eonix persistence (EPIC-08, EPIC-09, EPIC-10)
    Task SaveSessionSummaryAsync(string campaignId, SessionSummaryDto summary, CancellationToken cancellationToken = default);
    Task<SessionSummaryDto?> GetSessionSummaryAsync(string campaignId, string sessionId, SummaryVisibilityMode mode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SessionSummaryDto>> ListSessionSummariesAsync(string campaignId, string? sessionId = null, CancellationToken cancellationToken = default);
}
