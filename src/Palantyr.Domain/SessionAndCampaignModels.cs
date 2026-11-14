using Palantyr.Contracts;

namespace Palantyr.Domain;

/// <summary>
/// Campaign metadata persisted in both `Palantyr_System` registry and `Palantyr_Campaign_<Id>`.
/// </summary>
public sealed class CampaignDocument
{
    public required string Id { get; init; }
    public required string DatabaseName { get; init; }
    public required string Name { get; set; }
    public string? SubtitleOrSettingName { get; set; }
    public string? WorldSummary { get; set; }
    public string? CalendarSystemDescription { get; set; }
    public PalantyrLanguage PrimaryLanguage { get; set; } = PalantyrLanguage.Spanish;
    public string? PreferredMicrophoneDeviceId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastOpenedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastBackupAtUtc { get; set; }
    public DateTimeOffset? LastMeaningfulChangeAtUtc { get; set; }
    public List<PartyMemberDto> PartyRoster { get; set; } = [];

    /// <summary>
    /// Activity-aware monthly backup reminder rule (NFR-DATA-009, AC-DATA-005).
    /// Returns true ONLY if meaningful changes occurred since the last backup AND >= 30 days elapsed since last backup/creation.
    /// </summary>
    public bool ShouldRecommendMonthlyBackup(DateTimeOffset nowUtc, SessionRecordingState currentSessionState)
    {
        if (currentSessionState is SessionRecordingState.Recording or SessionRecordingState.Paused)
        {
            return false; // Never interrupt active play (NFR-DATA-009)
        }

        if (LastMeaningfulChangeAtUtc is null)
        {
            return false;
        }

        if (LastBackupAtUtc is not null && LastMeaningfulChangeAtUtc <= LastBackupAtUtc)
        {
            return false; // No meaningful changes since last backup (AC-DATA-005)
        }

        var baselineDate = LastBackupAtUtc ?? CreatedAtUtc;
        return (nowUtc - baselineDate) >= TimeSpan.FromDays(30);
    }
}

/// <summary>
/// Session record reconciled from the authoritative `.pseg` WAL (ADR-003, Kaede Finding 1).
/// </summary>
public sealed class SessionDocument
{
    public required string Id { get; init; }
    public required string CampaignId { get; init; }
    public required int SessionNumber { get; init; }
    public required string Title { get; set; }
    public SessionRecordingState RecordingState { get; set; } = SessionRecordingState.Recording;
    public SubsystemProcessingState TranscriptionState { get; set; } = SubsystemProcessingState.Pending;
    public SubsystemProcessingState MemoryProcessingState { get; set; } = SubsystemProcessingState.Pending;
    public DateTimeOffset StartedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAtUtc { get; set; }
    public long TotalRecordedDurationMs { get; set; }
    public bool AudioCleanedUp { get; set; }
    public List<AudioSegmentWalEntry> Segments { get; set; } = [];
}