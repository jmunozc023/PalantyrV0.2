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
/// Per-campaign store (`Palantyr_Campaign_<Id>` DB) for sessions, lore, and Universal Memory entities.
/// </summary>
public interface ICampaignMemoryStore
{
    Task UpsertSessionFromWalAsync(string campaignId, SessionDocument session, CancellationToken cancellationToken = default);
    Task<SessionDocument?> GetSessionAsync(string campaignId, string sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SessionDocument>> ListSessionsAsync(string campaignId, CancellationToken cancellationToken = default);
    Task SaveMemoryEntityAsync(string campaignId, MemoryEntityDocument entity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PartySafeMemoryView>> QueryPartySafeMemoryAsync(string campaignId, QueryVisibilityContext visibilityContext, MemoryEntityType? filterType = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryEntityDocument>> QueryDmFullMemoryAsync(string campaignId, MemoryEntityType? filterType = null, CancellationToken cancellationToken = default);
    Task<string> CreateCampaignBackupAsync(string campaignId, string targetDirectory, bool includeRetainedAudio, CancellationToken cancellationToken = default);
}
