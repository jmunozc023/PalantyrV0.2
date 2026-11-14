using System.Text.Json;
using System.Text.Json.Serialization;
using Palantyr.Contracts;
using Palantyr.Domain;

namespace Palantyr.Data.Raven;

/// <summary>
/// Durable implementation of <see cref="ISystemStore"/> backed by the `Palantyr_System` database
/// and `<BaseDataDirectory>/System/` local storage (ADR-002, EPIC-01, EPIC-02, EPIC-11).
/// </summary>
public sealed class RavenSystemStore : ISystemStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly RavenEmbeddedConfiguration _config;
    private readonly ICampaignMemoryStore _campaignMemoryStore;

    public RavenSystemStore()
        : this(RavenEmbeddedConfiguration.Default, null)
    {
    }

    public RavenSystemStore(string baseDataDirectory, ICampaignMemoryStore? campaignMemoryStore = null)
        : this(new RavenEmbeddedConfiguration(baseDataDirectory), campaignMemoryStore)
    {
    }

    public RavenSystemStore(RavenEmbeddedConfiguration config, ICampaignMemoryStore? campaignMemoryStore = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _config.ValidateGuardrails();
        _campaignMemoryStore = campaignMemoryStore ?? new RavenCampaignMemoryStore(_config);
        Directory.CreateDirectory(_config.GetSystemDirectoryPath());
        Directory.CreateDirectory(_config.GetCampaignsRootDirectoryPath());
    }

    public RavenEmbeddedConfiguration Configuration => _config;

    public async Task<AppSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var settingsPath = GetSettingsFilePath();
            if (!File.Exists(settingsPath))
            {
                return CreateDefaultSettings();
            }

            var json = await File.ReadAllTextAsync(settingsPath, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<AppSettingsDto>(json, JsonOptions) ?? CreateDefaultSettings();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<AppSettingsDto> SaveSettingsAsync(AppSettingsDto settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_config.GetSystemDirectoryPath());
            var settingsPath = GetSettingsFilePath();
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            await WriteFileAtomicallyAsync(settingsPath, json, cancellationToken).ConfigureAwait(false);
            return settings;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<SystemAssessmentProfile?> GetLatestAssessmentAsync(CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var assessmentPath = GetAssessmentFilePath();
            if (!File.Exists(assessmentPath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(assessmentPath, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<SystemAssessmentProfile>(json, JsonOptions);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task SaveAssessmentAsync(SystemAssessmentProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_config.GetSystemDirectoryPath());
            var assessmentPath = GetAssessmentFilePath();
            var json = JsonSerializer.Serialize(profile, JsonOptions);
            await WriteFileAtomicallyAsync(assessmentPath, json, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyList<CampaignSummaryDto>> ListCampaignsAsync(CancellationToken cancellationToken = default)
    {
        List<CampaignDocument> campaigns;
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            campaigns = await LoadAllCampaignsInternalAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _mutex.Release();
        }

        var summaries = new List<CampaignSummaryDto>(campaigns.Count);
        foreach (var campaign in campaigns.OrderByDescending(c => c.LastOpenedAtUtc))
        {
            var sessions = await _campaignMemoryStore.ListSessionsAsync(campaign.Id, cancellationToken).ConfigureAwait(false);
            var entities = await _campaignMemoryStore.QueryDmFullMemoryAsync(campaign.Id, null, cancellationToken).ConfigureAwait(false);

            int needsAttentionCount = entities.Count(e =>
                e.Confidence == ConfidenceLevel.LowOrUncertain ||
                e.Visibility == VisibilityScope.Ambiguous ||
                e.KnowledgeState == KnowledgeState.UnknownOrUnresolved);

            bool hasUnbackedUpChanges = campaign.LastMeaningfulChangeAtUtc is not null &&
                (campaign.LastBackupAtUtc is null || campaign.LastMeaningfulChangeAtUtc > campaign.LastBackupAtUtc);

            summaries.Add(new CampaignSummaryDto(
                CampaignId: campaign.Id,
                DatabaseName: campaign.DatabaseName,
                Name: campaign.Name,
                SubtitleOrSettingName: campaign.SubtitleOrSettingName,
                WorldSummary: campaign.WorldSummary,
                CalendarSystemDescription: campaign.CalendarSystemDescription,
                PrimaryLanguage: campaign.PrimaryLanguage,
                CreatedAtUtc: campaign.CreatedAtUtc,
                LastOpenedAtUtc: campaign.LastOpenedAtUtc,
                LastBackupAtUtc: campaign.LastBackupAtUtc,
                HasUnbackedUpChanges: hasUnbackedUpChanges,
                SessionCount: sessions.Count,
                PartyMemberCount: campaign.PartyRoster.Count,
                NeedsAttentionCount: needsAttentionCount));
        }

        return summaries;
    }

    public async Task<CampaignDocument> CreateCampaignAsync(CreateCampaignRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Campaign name is required.", nameof(request));
        }

        var now = DateTimeOffset.UtcNow;
        var campaignId = GenerateCampaignId(request.Name);
        var databaseName = RavenEmbeddedConfiguration.GetCampaignDatabaseName(campaignId);

        var normalizedRoster = NormalizeRoster(request.InitialPartyRoster ?? [], existingRoster: null, now);

        var campaign = new CampaignDocument
        {
            Id = campaignId,
            DatabaseName = databaseName,
            Name = request.Name.Trim(),
            SubtitleOrSettingName = request.SubtitleOrSettingName,
            WorldSummary = request.WorldSummary,
            CalendarSystemDescription = request.CalendarSystemDescription,
            PrimaryLanguage = request.PrimaryLanguage,
            PreferredMicrophoneDeviceId = request.PreferredMicrophoneDeviceId,
            CreatedAtUtc = now,
            LastOpenedAtUtc = now,
            LastBackupAtUtc = null,
            LastMeaningfulChangeAtUtc = now,
            PartyRoster = normalizedRoster
        };

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var campaignDir = _config.GetCampaignDirectoryPath(campaignId);
            Directory.CreateDirectory(campaignDir);
            Directory.CreateDirectory(Path.Combine(campaignDir, "Sessions"));
            Directory.CreateDirectory(Path.Combine(campaignDir, "Memory"));
            Directory.CreateDirectory(Path.Combine(campaignDir, "Revisions"));
            Directory.CreateDirectory(Path.Combine(campaignDir, "Audio"));

            var campaigns = await LoadAllCampaignsInternalAsync(cancellationToken).ConfigureAwait(false);
            campaigns.RemoveAll(c => string.Equals(c.Id, campaignId, StringComparison.OrdinalIgnoreCase));
            campaigns.Add(campaign);
            await SaveAllCampaignsInternalAsync(campaigns, cancellationToken).ConfigureAwait(false);
            await SaveCampaignMetadataSnapshotAsync(campaign, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _mutex.Release();
        }

        // Seed initial PartyRoster as Character MemoryEntityDocument records (FR-CAM-001..006)
        foreach (var member in normalizedRoster)
        {
            var charDoc = BuildCharacterMemoryDocument(member, now);
            await _campaignMemoryStore.SaveMemoryEntityAsync(campaignId, charDoc, cancellationToken).ConfigureAwait(false);
        }

        // Seed initial WorldLore entries into ICampaignMemoryStore (FR-SETUP-001..006)
        if (request.InitialWorldLore is { Count: > 0 })
        {
            foreach (var lore in request.InitialWorldLore)
            {
                var loreDoc = BuildWorldLoreMemoryDocument(lore, now);
                await _campaignMemoryStore.SaveMemoryEntityAsync(campaignId, loreDoc, cancellationToken).ConfigureAwait(false);
            }
        }

        return campaign;
    }

    public async Task<CampaignDocument?> GetCampaignAsync(string campaignId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(campaignId))
        {
            return null;
        }

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var campaigns = await LoadAllCampaignsInternalAsync(cancellationToken).ConfigureAwait(false);
            var match = campaigns.FirstOrDefault(c => string.Equals(c.Id, campaignId, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                // Synchronize with per-campaign metadata snapshot if a backup or session updated timestamps
                var snapshotPath = GetCampaignSnapshotFilePath(campaignId);
                if (File.Exists(snapshotPath))
                {
                    var snapshotJson = await File.ReadAllTextAsync(snapshotPath, cancellationToken).ConfigureAwait(false);
                    var snapshot = JsonSerializer.Deserialize<CampaignDocument>(snapshotJson, JsonOptions);
                    if (snapshot is not null)
                    {
                        if (snapshot.LastBackupAtUtc is not null &&
                            (match.LastBackupAtUtc is null || snapshot.LastBackupAtUtc > match.LastBackupAtUtc))
                        {
                            match.LastBackupAtUtc = snapshot.LastBackupAtUtc;
                        }

                        if (snapshot.LastMeaningfulChangeAtUtc is not null &&
                            (match.LastMeaningfulChangeAtUtc is null || snapshot.LastMeaningfulChangeAtUtc > match.LastMeaningfulChangeAtUtc))
                        {
                            match.LastMeaningfulChangeAtUtc = snapshot.LastMeaningfulChangeAtUtc;
                        }
                    }
                }
            }

            return match;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<CampaignDocument> UpdatePartyRosterAsync(
        string campaignId,
        IReadOnlyList<PartyMemberDto> roster,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentNullException.ThrowIfNull(roster);

        var now = DateTimeOffset.UtcNow;
        CampaignDocument updatedCampaign;
        List<PartyMemberDto> normalizedRoster;

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var campaigns = await LoadAllCampaignsInternalAsync(cancellationToken).ConfigureAwait(false);
            var existing = campaigns.FirstOrDefault(c => string.Equals(c.Id, campaignId, StringComparison.OrdinalIgnoreCase))
                ?? throw new KeyNotFoundException($"Campaign '{campaignId}' was not found.");

            normalizedRoster = NormalizeRoster(roster, existing.PartyRoster, now);
            existing.PartyRoster = normalizedRoster;
            existing.LastMeaningfulChangeAtUtc = now;
            existing.LastOpenedAtUtc = now;
            updatedCampaign = existing;

            await SaveAllCampaignsInternalAsync(campaigns, cancellationToken).ConfigureAwait(false);
            await SaveCampaignMetadataSnapshotAsync(updatedCampaign, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _mutex.Release();
        }

        foreach (var member in normalizedRoster)
        {
            var charDoc = BuildCharacterMemoryDocument(member, now);
            await _campaignMemoryStore.SaveMemoryEntityAsync(campaignId, charDoc, cancellationToken).ConfigureAwait(false);
        }

        return updatedCampaign;
    }

    public async Task<string?> DeleteCampaignSafelyAsync(
        string campaignId,
        CampaignDeletionOption option,
        string backupDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);

        if (option == CampaignDeletionOption.Cancel)
        {
            return null;
        }

        var existing = await GetCampaignAsync(campaignId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        string? backupFilePath = null;
        if (option == CampaignDeletionOption.CreateBackupAndDelete)
        {
            var targetBackupDir = string.IsNullOrWhiteSpace(backupDirectory)
                ? _config.GetBackupsDirectoryPath()
                : backupDirectory;

            backupFilePath = await _campaignMemoryStore
                .CreateCampaignBackupAsync(campaignId, targetBackupDir, includeRetainedAudio: true, cancellationToken)
                .ConfigureAwait(false);
        }

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var campaigns = await LoadAllCampaignsInternalAsync(cancellationToken).ConfigureAwait(false);
            campaigns.RemoveAll(c => string.Equals(c.Id, campaignId, StringComparison.OrdinalIgnoreCase));
            await SaveAllCampaignsInternalAsync(campaigns, cancellationToken).ConfigureAwait(false);

            var campaignDir = _config.GetCampaignDirectoryPath(campaignId);
            if (Directory.Exists(campaignDir))
            {
                Directory.Delete(campaignDir, recursive: true);
            }
        }
        finally
        {
            _mutex.Release();
        }

        return backupFilePath;
    }

    private static AppSettingsDto CreateDefaultSettings() =>
        new(
            UiLanguage: PalantyrLanguage.English,
            ActiveProcessingMode: ProcessingMode.LiveBalanced,
            PausePreference: PauseTranscriptionBehavior.Ask,
            SelectedAudioDeviceId: null,
            KeepOriginalSessionAudio: false,
            KeepAudioWarningAcknowledged: false,
            ReduceAnimations: false,
            NominalSegmentDurationMinutes: 15);

    private static string GenerateCampaignId(string name)
    {
        var slugChars = name.Trim().ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == ' ')
            .Select(c => c == ' ' ? '-' : c)
            .ToArray();

        var slug = new string(slugChars).Trim('-');
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "campaign";
        }

        if (slug.Length > 20)
        {
            slug = slug[..20].TrimEnd('-');
        }

        var shortGuid = Guid.NewGuid().ToString("N")[..8];
        return $"{slug}-{shortGuid}";
    }

    private static List<PartyMemberDto> NormalizeRoster(
        IReadOnlyList<PartyMemberDto> incomingRoster,
        IReadOnlyList<PartyMemberDto>? existingRoster,
        DateTimeOffset nowUtc)
    {
        var result = new List<PartyMemberDto>(incomingRoster.Count);
        foreach (var member in incomingRoster)
        {
            var charId = string.IsNullOrWhiteSpace(member.CharacterId)
                ? $"characters/{Guid.NewGuid():N}"
                : member.CharacterId.Trim();

            var previous = existingRoster?.FirstOrDefault(m =>
                string.Equals(m.CharacterId, charId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.CharacterName, member.CharacterName, StringComparison.OrdinalIgnoreCase));

            var history = new List<PartyMembershipHistoryEntry>();
            if (previous?.MembershipHistory is { Count: > 0 })
            {
                history.AddRange(previous.MembershipHistory);
            }

            if (member.MembershipHistory is { Count: > 0 })
            {
                foreach (var entry in member.MembershipHistory)
                {
                    if (!history.Any(h => h.Status == entry.Status && h.ChangedAtUtc == entry.ChangedAtUtc))
                    {
                        history.Add(entry);
                    }
                }
            }

            if (history.Count == 0)
            {
                history.Add(new PartyMembershipHistoryEntry(
                    Status: member.CurrentStatus,
                    ChangedAtUtc: nowUtc,
                    SessionId: null,
                    CampaignDateContext: "Campaign Setup",
                    ReasonNote: "Initial party roster assignment"));
            }
            else if (history[^1].Status != member.CurrentStatus)
            {
                history.Add(new PartyMembershipHistoryEntry(
                    Status: member.CurrentStatus,
                    ChangedAtUtc: nowUtc,
                    SessionId: null,
                    CampaignDateContext: null,
                    ReasonNote: $"Membership status changed to {member.CurrentStatus}"));
            }

            result.Add(member with
            {
                CharacterId = charId,
                MembershipHistory = history
            });
        }

        return result;
    }

    private static MemoryEntityDocument BuildCharacterMemoryDocument(PartyMemberDto member, DateTimeOffset nowUtc)
    {
        var entityId = member.CharacterId.StartsWith("characters/", StringComparison.OrdinalIgnoreCase)
            ? member.CharacterId
            : $"characters/{RavenEmbeddedConfiguration.SanitizeId(member.CharacterId)}";

        var historyItems = member.MembershipHistory
            .Select((h, idx) => new MemoryHistoryItem(
                HistoryItemId: $"hist-char-{RavenEmbeddedConfiguration.SanitizeId(member.CharacterId)}-{idx + 1}",
                Description: $"Party membership status: {h.Status} ({member.RaceOrSpecies} {member.ClassOrArchetype})".Trim(),
                KnowledgeState: KnowledgeState.CampaignTruth,
                Confidence: ConfidenceLevel.DMConfirmed,
                Visibility: VisibilityScope.PartyKnown,
                ValidFromSessionId: h.SessionId,
                ValidToSessionId: null,
                RelativeTimeContext: h.CampaignDateContext ?? "Campaign Setup",
                SupersededByHistoryItemId: null,
                RecordedAtUtc: h.ChangedAtUtc))
            .ToList();

        var roleDescriptor = member.IsPlayerCharacter ? "PC" : "Companion/NPC";
        var summary = $"{member.CharacterName} — {member.RaceOrSpecies} {member.ClassOrArchetype} [{roleDescriptor}, Status: {member.CurrentStatus}]".Trim();

        return new MemoryEntityDocument
        {
            Id = entityId,
            EntityType = MemoryEntityType.Character,
            CanonicalName = member.CharacterName,
            SummaryDescription = summary,
            Visibility = VisibilityScope.PartyKnown,
            KnowledgeState = KnowledgeState.CampaignTruth,
            Confidence = ConfidenceLevel.DMConfirmed,
            InternalConfidenceScore = 1.0,
            FirstAppearance = new FirstAppearanceRef(
                RealDateUtc: nowUtc,
                CampaignDateText: null,
                RelativeTimeContext: "Campaign Setup",
                SessionId: null,
                LocationEntityId: null,
                TimestampMs: 0),
            StateAndBeliefHistory = historyItems,
            Sources =
            [
                new ProvenanceRef(
                    SourceType: ProvenanceSourceType.CampaignSetup,
                    SessionId: null,
                    SegmentId: null,
                    SpanId: null,
                    StartTimestampMs: null,
                    EndTimestampMs: null,
                    RecordedAtUtc: nowUtc,
                    NoteOrContext: $"Party Roster ({member.CurrentStatus})")
            ]
        };
    }

    private static MemoryEntityDocument BuildWorldLoreMemoryDocument(WorldLoreEntryDto lore, DateTimeOffset nowUtc)
    {
        var rawId = string.IsNullOrWhiteSpace(lore.LoreId) ? Guid.NewGuid().ToString("N")[..10] : lore.LoreId.Trim();
        var entityId = rawId.StartsWith("lore/", StringComparison.OrdinalIgnoreCase)
            ? rawId
            : $"lore/{RavenEmbeddedConfiguration.SanitizeId(rawId)}";

        var createdAt = lore.CreatedAtUtc == default ? nowUtc : lore.CreatedAtUtc;

        return new MemoryEntityDocument
        {
            Id = entityId,
            EntityType = MemoryEntityType.WorldLore,
            CanonicalName = lore.Title,
            SummaryDescription = lore.Content,
            DmPrivateNotes = lore.Visibility == VisibilityScope.DMPrivate ? lore.Content : null,
            Visibility = lore.Visibility,
            KnowledgeState = lore.KnowledgeState,
            Confidence = lore.Confidence,
            InternalConfidenceScore = lore.Confidence == ConfidenceLevel.DMConfirmed ? 1.0 : 0.9,
            FirstAppearance = new FirstAppearanceRef(
                RealDateUtc: createdAt,
                CampaignDateText: null,
                RelativeTimeContext: "Campaign Setup",
                SessionId: null,
                LocationEntityId: null,
                TimestampMs: null),
            StateAndBeliefHistory =
            [
                new MemoryHistoryItem(
                    HistoryItemId: $"hist-lore-{RavenEmbeddedConfiguration.SanitizeId(rawId)}-1",
                    Description: lore.Content,
                    KnowledgeState: lore.KnowledgeState,
                    Confidence: lore.Confidence,
                    Visibility: lore.Visibility,
                    ValidFromSessionId: null,
                    ValidToSessionId: null,
                    RelativeTimeContext: "Campaign Setup",
                    SupersededByHistoryItemId: null,
                    RecordedAtUtc: createdAt)
            ],
            Sources =
            [
                new ProvenanceRef(
                    SourceType: lore.SourceType,
                    SessionId: null,
                    SegmentId: null,
                    SpanId: null,
                    StartTimestampMs: null,
                    EndTimestampMs: null,
                    RecordedAtUtc: createdAt,
                    NoteOrContext: $"Category: {lore.Category}")
            ]
        };
    }

    private string GetSettingsFilePath() => Path.Combine(_config.GetSystemDirectoryPath(), "settings.json");

    private string GetAssessmentFilePath() => Path.Combine(_config.GetSystemDirectoryPath(), "assessment.json");

    private string GetCampaignsCatalogFilePath() => Path.Combine(_config.GetSystemDirectoryPath(), "campaigns.json");

    private string GetCampaignSnapshotFilePath(string campaignId) =>
        Path.Combine(_config.GetCampaignDirectoryPath(campaignId), "campaign.json");

    private async Task<List<CampaignDocument>> LoadAllCampaignsInternalAsync(CancellationToken cancellationToken)
    {
        var path = GetCampaignsCatalogFilePath();
        if (!File.Exists(path))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        var list = JsonSerializer.Deserialize<List<CampaignDocument>>(json, JsonOptions) ?? [];

        foreach (var campaign in list)
        {
            var snapshotPath = GetCampaignSnapshotFilePath(campaign.Id);
            if (File.Exists(snapshotPath))
            {
                var snapshotJson = await File.ReadAllTextAsync(snapshotPath, cancellationToken).ConfigureAwait(false);
                var snapshot = JsonSerializer.Deserialize<CampaignDocument>(snapshotJson, JsonOptions);
                if (snapshot is not null)
                {
                    if (snapshot.LastBackupAtUtc is not null &&
                        (campaign.LastBackupAtUtc is null || snapshot.LastBackupAtUtc > campaign.LastBackupAtUtc))
                    {
                        campaign.LastBackupAtUtc = snapshot.LastBackupAtUtc;
                    }

                    if (snapshot.LastMeaningfulChangeAtUtc is not null &&
                        (campaign.LastMeaningfulChangeAtUtc is null || snapshot.LastMeaningfulChangeAtUtc > campaign.LastMeaningfulChangeAtUtc))
                    {
                        campaign.LastMeaningfulChangeAtUtc = snapshot.LastMeaningfulChangeAtUtc;
                    }
                }
            }
        }

        return list;
    }

    private async Task SaveAllCampaignsInternalAsync(List<CampaignDocument> campaigns, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_config.GetSystemDirectoryPath());
        var path = GetCampaignsCatalogFilePath();
        var json = JsonSerializer.Serialize(campaigns, JsonOptions);
        await WriteFileAtomicallyAsync(path, json, cancellationToken).ConfigureAwait(false);
    }

    private async Task SaveCampaignMetadataSnapshotAsync(CampaignDocument campaign, CancellationToken cancellationToken)
    {
        var campaignDir = _config.GetCampaignDirectoryPath(campaign.Id);
        Directory.CreateDirectory(campaignDir);
        var snapshotPath = GetCampaignSnapshotFilePath(campaign.Id);
        var json = JsonSerializer.Serialize(campaign, JsonOptions);
        await WriteFileAtomicallyAsync(snapshotPath, json, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task WriteFileAtomicallyAsync(string targetPath, string content, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{targetPath}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(tempPath, content, cancellationToken).ConfigureAwait(false);
        File.Move(tempPath, targetPath, overwrite: true);
    }
}