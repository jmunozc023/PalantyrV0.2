namespace Palantyr.Contracts;

/// <summary>
/// Audio input device descriptor (FR-REC-001, FR-SYS-002).
/// </summary>
public sealed record AudioInputDeviceInfo(
    string DeviceId,
    string FriendlyName,
    int Channels,
    int SampleRateHz,
    bool IsDefault,
    bool IsAvailable);

/// <summary>
/// Result of pre-session microphone signal validation (FR-REC-001).
/// </summary>
public sealed record MicrophoneValidationResult(
    string DeviceId,
    bool IsAvailable,
    bool HasDetectableSignal,
    double PeakLevelDb,
    string DiagnosticMessageEn,
    string DiagnosticMessageEs);

/// <summary>
/// First-run and manual hardware/system assessment profile (Section 27, FR-SYS-001..005).
/// </summary>
public sealed record SystemAssessmentProfile(
    string Id,
    DateTimeOffset AssessedAtUtc,
    string WindowsVersion,
    string CpuModel,
    int LogicalCores,
    double TotalRamGb,
    double AvailableRamGb,
    string? GpuModel,
    double DedicatedVramGb,
    double FreeStorageGb,
    bool HasCompatibleGpuAcceleration,
    ProcessingMode RecommendedMode,
    string RecommendationReasonEn,
    string RecommendationReasonEs,
    IReadOnlyList<AudioInputDeviceInfo> AudioDevices);

/// <summary>
/// Global and campaign-relevant application settings (Section 38 V1 REQUIRED - Settings).
/// </summary>
public sealed record AppSettingsDto(
    PalantyrLanguage UiLanguage,
    ProcessingMode ActiveProcessingMode,
    PauseTranscriptionBehavior PausePreference,
    string? SelectedAudioDeviceId,
    bool KeepOriginalSessionAudio,
    bool KeepAudioWarningAcknowledged,
    bool ReduceAnimations,
    int NominalSegmentDurationMinutes = 15);

/// <summary>
/// Party character roster entry with historical membership tracking (Section 15, FR-CAM-001..006).
/// </summary>
public sealed record PartyMemberDto(
    string CharacterId,
    string CharacterName,
    string RaceOrSpecies,
    string ClassOrArchetype,
    string? ControlledByPlayerName,
    bool IsPlayerCharacter,
    PartyMembershipStatus CurrentStatus,
    IReadOnlyList<PartyMembershipHistoryEntry> MembershipHistory);

public sealed record PartyMembershipHistoryEntry(
    PartyMembershipStatus Status,
    DateTimeOffset ChangedAtUtc,
    string? SessionId,
    string? CampaignDateContext,
    string? ReasonNote);

/// <summary>
/// Initial or ongoing World / General Lore entry (Section 22, FR-SETUP-001..007).
/// </summary>
public sealed record WorldLoreEntryDto(
    string LoreId,
    string Title,
    string Category,
    string Content,
    VisibilityScope Visibility,
    KnowledgeState KnowledgeState,
    ConfidenceLevel Confidence,
    ProvenanceSourceType SourceType,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// Request for the guided Create Campaign wizard (Section 28).
/// </summary>
public sealed record CreateCampaignRequest(
    string Name,
    string? SubtitleOrSettingName,
    string? WorldSummary,
    string? CalendarSystemDescription,
    PalantyrLanguage PrimaryLanguage,
    IReadOnlyList<PartyMemberDto> InitialPartyRoster,
    IReadOnlyList<WorldLoreEntryDto> InitialWorldLore,
    string? PreferredMicrophoneDeviceId);

/// <summary>
/// Summary card for Campaign selection & Campaign Home (Section 28, 30).
/// </summary>
public sealed record CampaignSummaryDto(
    string CampaignId,
    string DatabaseName,
    string Name,
    string? SubtitleOrSettingName,
    string? WorldSummary,
    string? CalendarSystemDescription,
    PalantyrLanguage PrimaryLanguage,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastOpenedAtUtc,
    DateTimeOffset? LastBackupAtUtc,
    bool HasUnbackedUpChanges,
    int SessionCount,
    int PartyMemberCount,
    int NeedsAttentionCount);
