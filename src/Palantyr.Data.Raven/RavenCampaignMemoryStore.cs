using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Palantyr.Contracts;
using Palantyr.Domain;

namespace Palantyr.Data.Raven;

/// <summary>
/// Per-campaign store (`Palantyr_Campaign_<Id>` DB) for sessions, lore, and Universal Memory entities.
/// Enforces Pre-Generation Visibility Gatekeeping (ADR-006, Kaede Finding 3) and `.palantyr-backup`
/// container creation with SHA-256 checksums and optional retained audio (ADR-009, AC-DATA-003, AC-DATA-007).
/// </summary>
public sealed class RavenCampaignMemoryStore : ICampaignMemoryStore
{
    public const string BackupPrivacyNoticeEn =
        "PRIVACY NOTICE (NFR-PRIV-007): This campaign backup contains full DM-Private secrets, hidden lore, recognition variants, and session transcripts. Do not share with players.";

    public const string BackupPrivacyNoticeEs =
        "AVISO DE PRIVACIDAD (NFR-PRIV-007): Esta copia de seguridad contiene secretos privados del DM, lore oculto, variantes de reconocimiento y transcripciones. No compartir con los jugadores.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly RavenEmbeddedConfiguration _config;

    public RavenCampaignMemoryStore()
        : this(RavenEmbeddedConfiguration.Default)
    {
    }

    public RavenCampaignMemoryStore(string baseDataDirectory)
        : this(new RavenEmbeddedConfiguration(baseDataDirectory))
    {
    }

    public RavenCampaignMemoryStore(RavenEmbeddedConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _config.ValidateGuardrails();
        Directory.CreateDirectory(_config.GetCampaignsRootDirectoryPath());
    }

    public RavenEmbeddedConfiguration Configuration => _config;

    public async Task UpsertSessionFromWalAsync(
        string campaignId,
        SessionDocument session,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(session.Id);

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var sessions = await LoadSessionsInternalAsync(campaignId, cancellationToken).ConfigureAwait(false);
            var existingIndex = sessions.FindIndex(s => string.Equals(s.Id, session.Id, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                sessions[existingIndex] = session;
            }
            else
            {
                sessions.Add(session);
            }

            await SaveSessionsInternalAsync(campaignId, sessions, cancellationToken).ConfigureAwait(false);
            await TouchCampaignMeaningfulChangeInternalAsync(campaignId, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<SessionDocument?> GetSessionAsync(
        string campaignId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(campaignId) || string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var sessions = await LoadSessionsInternalAsync(campaignId, cancellationToken).ConfigureAwait(false);
            return sessions.FirstOrDefault(s => string.Equals(s.Id, sessionId, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyList<SessionDocument>> ListSessionsAsync(
        string campaignId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(campaignId))
        {
            return [];
        }

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var sessions = await LoadSessionsInternalAsync(campaignId, cancellationToken).ConfigureAwait(false);
            return sessions
                .OrderBy(s => s.SessionNumber)
                .ThenBy(s => s.StartedAtUtc)
                .ToList();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task SaveMemoryEntityAsync(
        string campaignId,
        MemoryEntityDocument entity,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Id);

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entities = await LoadEntitiesInternalAsync(campaignId, cancellationToken).ConfigureAwait(false);
            var existingIndex = entities.FindIndex(e => string.Equals(e.Id, entity.Id, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                entities[existingIndex] = entity;
            }
            else
            {
                entities.Add(entity);
            }

            await SaveEntitiesInternalAsync(campaignId, entities, cancellationToken).ConfigureAwait(false);
            await AppendDocumentRevisionInternalAsync(campaignId, entity, cancellationToken).ConfigureAwait(false);
            await TouchCampaignMeaningfulChangeInternalAsync(campaignId, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _mutex.Release();
        }
    }

    /// <summary>
    /// Enforces ADR-006 and Kaede Finding 3: applies both document-level visibility filtering AND
    /// item-level projection (`entity.ToPartySafeView(visibilityContext)`) so `DMPrivate` notes,
    /// `RecognitionVariants`, and unauthorized embedded `StateAndBeliefHistory` items are stripped
    /// prior to returning <see cref="PartySafeMemoryView"/>.
    /// </summary>
    public async Task<IReadOnlyList<PartySafeMemoryView>> QueryPartySafeMemoryAsync(
        string campaignId,
        QueryVisibilityContext visibilityContext,
        MemoryEntityType? filterType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentNullException.ThrowIfNull(visibilityContext);

        List<MemoryEntityDocument> entities;
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            entities = await LoadEntitiesInternalAsync(campaignId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _mutex.Release();
        }

        var results = new List<PartySafeMemoryView>();
        foreach (var entity in entities)
        {
            if (filterType.HasValue && entity.EntityType != filterType.Value)
            {
                continue;
            }

            // Document-level gate (ADR-006)
            if (!visibilityContext.Permits(entity.Visibility, entity.AuthorizedCharacterIds))
            {
                continue;
            }

            if (!visibilityContext.IncludeDmPrivate && entity.KnowledgeState == KnowledgeState.DMPrivateTruth)
            {
                continue;
            }

            // Sub-collection & field-level stripping projection (Kaede Finding 3)
            var safeView = entity.ToPartySafeView(visibilityContext);
            if (safeView is not null)
            {
                results.Add(safeView);
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<MemoryEntityDocument>> QueryDmFullMemoryAsync(
        string campaignId,
        MemoryEntityType? filterType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entities = await LoadEntitiesInternalAsync(campaignId, cancellationToken).ConfigureAwait(false);
            if (filterType.HasValue)
            {
                return entities.Where(e => e.EntityType == filterType.Value).ToList();
            }

            return entities;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<MemoryEntityDocument?> GetMemoryEntityAsync(
        string campaignId,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entities = await LoadEntitiesInternalAsync(campaignId, cancellationToken).ConfigureAwait(false);
            return entities.FirstOrDefault(e => string.Equals(e.Id, entityId, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyList<MemoryEntityDocument>> ListMemoryEntitiesAsync(
        string campaignId,
        MemoryEntityType? filterType = null,
        string? searchQuery = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entities = await LoadEntitiesInternalAsync(campaignId, cancellationToken).ConfigureAwait(false);
            var query = entities.AsEnumerable();

            if (filterType.HasValue)
            {
                query = query.Where(e => e.EntityType == filterType.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                var q = searchQuery.Trim();
                query = query.Where(e =>
                    e.CanonicalName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    e.Aliases.Any(a => a.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    e.Titles.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    e.SummaryDescription.Contains(q, StringComparison.OrdinalIgnoreCase));
            }

            return query.OrderBy(e => e.CanonicalName).ToList();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task DeleteMemoryEntityAsync(
        string campaignId,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entities = await LoadEntitiesInternalAsync(campaignId, cancellationToken).ConfigureAwait(false);
            int removed = entities.RemoveAll(e => string.Equals(e.Id, entityId, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                await SaveEntitiesInternalAsync(campaignId, entities, cancellationToken).ConfigureAwait(false);
                await TouchCampaignMeaningfulChangeInternalAsync(campaignId, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task SaveResolutionCandidateAsync(
        string campaignId,
        EntityResolutionCandidateDto candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentNullException.ThrowIfNull(candidate);

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var candidates = await LoadResolutionCandidatesInternalAsync(campaignId, cancellationToken).ConfigureAwait(false);
            var index = candidates.FindIndex(c => string.Equals(c.CandidateId, candidate.CandidateId, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                candidates[index] = candidate;
            }
            else
            {
                candidates.Add(candidate);
            }

            await SaveResolutionCandidatesInternalAsync(campaignId, candidates, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyList<EntityResolutionCandidateDto>> ListResolutionCandidatesAsync(
        string campaignId,
        EntityResolutionStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var candidates = await LoadResolutionCandidatesInternalAsync(campaignId, cancellationToken).ConfigureAwait(false);
            if (statusFilter.HasValue)
            {
                return candidates.Where(c => c.Status == statusFilter.Value).OrderByDescending(c => c.DetectedAtUtc).ToList();
            }

            return candidates.OrderByDescending(c => c.DetectedAtUtc).ToList();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<EntityResolutionCandidateDto?> GetResolutionCandidateAsync(
        string campaignId,
        string candidateId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateId);

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var candidates = await LoadResolutionCandidatesInternalAsync(campaignId, cancellationToken).ConfigureAwait(false);
            return candidates.FirstOrDefault(c => string.Equals(c.CandidateId, candidateId, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task SaveConflictAsync(
        string campaignId,
        MemoryConflictDto conflict,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentNullException.ThrowIfNull(conflict);

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var conflicts = await LoadConflictsInternalAsync(campaignId, cancellationToken).ConfigureAwait(false);
            var index = conflicts.FindIndex(c => string.Equals(c.ConflictId, conflict.ConflictId, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                conflicts[index] = conflict;
            }
            else
            {
                conflicts.Add(conflict);
            }

            await SaveConflictsInternalAsync(campaignId, conflicts, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyList<MemoryConflictDto>> ListConflictsAsync(
        string campaignId,
        string? entityId = null,
        bool unresolvedOnly = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var conflicts = await LoadConflictsInternalAsync(campaignId, cancellationToken).ConfigureAwait(false);
            var query = conflicts.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(entityId))
            {
                query = query.Where(c => string.Equals(c.EntityId, entityId, StringComparison.OrdinalIgnoreCase));
            }

            if (unresolvedOnly)
            {
                query = query.Where(c => c.State == ConflictResolutionState.Unresolved);
            }

            return query.ToList();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<MemoryConflictDto?> GetConflictAsync(
        string campaignId,
        string conflictId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentException.ThrowIfNullOrWhiteSpace(conflictId);

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var conflicts = await LoadConflictsInternalAsync(campaignId, cancellationToken).ConfigureAwait(false);
            return conflicts.FirstOrDefault(c => string.Equals(c.ConflictId, conflictId, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyList<LocationHierarchyNodeDto>> GetLocationHierarchyTreeAsync(
        string campaignId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entities = await LoadEntitiesInternalAsync(campaignId, cancellationToken).ConfigureAwait(false);
            var locations = entities.Where(e => e.EntityType == MemoryEntityType.Location).ToList();
            var locationMap = locations.ToDictionary(l => l.Id, StringComparer.OrdinalIgnoreCase);

            (string Name, string? ParentId)? Resolver(string id) =>
                locationMap.TryGetValue(id, out var loc) ? (loc.CanonicalName, loc.LocationFacet?.ParentLocationId) : null;

            LocationHierarchyNodeDto BuildNode(MemoryEntityDocument loc)
            {
                var locType = loc.LocationFacet?.LocationType ?? LocationType.Settlement;
                var parentId = loc.LocationFacet?.ParentLocationId;
                var breadcrumb = LocationFacet.ComputeBreadcrumb(loc.Id, loc.CanonicalName, Resolver);

                var children = locations
                    .Where(c => string.Equals(c.LocationFacet?.ParentLocationId, loc.Id, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(c => c.CanonicalName)
                    .Select(BuildNode)
                    .ToList();

                return new LocationHierarchyNodeDto(
                    LocationId: loc.Id,
                    CanonicalName: loc.CanonicalName,
                    LocationType: locType,
                    ParentLocationId: parentId,
                    BreadcrumbPath: breadcrumb,
                    Children: children);
            }

            var rootLocations = locations
                .Where(l => string.IsNullOrWhiteSpace(l.LocationFacet?.ParentLocationId) ||
                            !locationMap.ContainsKey(l.LocationFacet.ParentLocationId))
                .OrderBy(l => l.CanonicalName)
                .Select(BuildNode)
                .ToList();

            return rootLocations;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task SaveTranscriptSegmentAsync(
        string campaignId,
        TranscriptSegmentDocument transcript,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentNullException.ThrowIfNull(transcript);
        ArgumentException.ThrowIfNullOrWhiteSpace(transcript.SegmentId);

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var transcripts = await LoadTranscriptsInternalAsync(campaignId, cancellationToken).ConfigureAwait(false);
            var existingIndex = transcripts.FindIndex(t =>
                string.Equals(t.SessionId, transcript.SessionId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(t.SegmentId, transcript.SegmentId, StringComparison.OrdinalIgnoreCase));

            if (existingIndex >= 0)
            {
                transcripts[existingIndex] = transcript;
            }
            else
            {
                transcripts.Add(transcript);
            }

            await SaveTranscriptsInternalAsync(campaignId, transcripts, cancellationToken).ConfigureAwait(false);
            await TouchCampaignMeaningfulChangeInternalAsync(campaignId, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<TranscriptSegmentDocument?> GetTranscriptSegmentAsync(
        string campaignId,
        string sessionId,
        string segmentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(campaignId) || string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(segmentId))
        {
            return null;
        }

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var transcripts = await LoadTranscriptsInternalAsync(campaignId, cancellationToken).ConfigureAwait(false);
            return transcripts.FirstOrDefault(t =>
                string.Equals(t.SessionId, sessionId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(t.SegmentId, segmentId, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyList<TranscriptSegmentDocument>> ListTranscriptSegmentsAsync(
        string campaignId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(campaignId) || string.IsNullOrWhiteSpace(sessionId))
        {
            return [];
        }

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var transcripts = await LoadTranscriptsInternalAsync(campaignId, cancellationToken).ConfigureAwait(false);
            return transcripts
                .Where(t => string.Equals(t.SessionId, sessionId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.SequenceNumber)
                .ToList();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task SaveNeedsAttentionItemAsync(
        string campaignId,
        NeedsAttentionItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(item.Id);

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var items = await LoadNeedsAttentionItemsInternalAsync(campaignId, cancellationToken).ConfigureAwait(false);
            var existingIndex = items.FindIndex(i => string.Equals(i.Id, item.Id, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                items[existingIndex] = item;
            }
            else
            {
                items.Add(item);
            }

            await SaveNeedsAttentionItemsInternalAsync(campaignId, items, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyList<NeedsAttentionItem>> ListNeedsAttentionItemsAsync(
        string campaignId,
        string? sessionId = null,
        bool unresolvedOnly = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(campaignId))
        {
            return [];
        }

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var items = await LoadNeedsAttentionItemsInternalAsync(campaignId, cancellationToken).ConfigureAwait(false);
            var query = items.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                query = query.Where(i => string.Equals(i.SessionId, sessionId, StringComparison.OrdinalIgnoreCase));
            }
            if (unresolvedOnly)
            {
                query = query.Where(i => !i.IsResolved);
            }
            return query.OrderBy(i => i.CreatedAtUtc).ToList();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task ResolveNeedsAttentionItemAsync(
        string campaignId,
        string itemId,
        string resolutionDecision,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var items = await LoadNeedsAttentionItemsInternalAsync(campaignId, cancellationToken).ConfigureAwait(false);
            var match = items.FirstOrDefault(i => string.Equals(i.Id, itemId, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                match.IsResolved = true;
                match.ResolutionDecision = resolutionDecision;
                match.ResolvedAtUtc = DateTimeOffset.UtcNow;
                await SaveNeedsAttentionItemsInternalAsync(campaignId, items, cancellationToken).ConfigureAwait(false);
                await TouchCampaignMeaningfulChangeInternalAsync(campaignId, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _mutex.Release();
        }
    }

    /// <summary>
    /// Creates a `.palantyr-backup` ZIP archive containing `manifest.json` (with SHA-256 checksums,
    /// `IncludesRetainedAudio` flag, and privacy notice `NFR-PRIV-007`), `campaign_data.json` / `campaign.ravendbdump`,
    /// and includes retained `.wav` audio files ONLY when <paramref name="includeRetainedAudio"/> is true (`ADR-009`, `AC-DATA-003`, `AC-DATA-007`).
    /// Updates `LastBackupAtUtc` on the campaign.
    /// </summary>
    public async Task<string> CreateCampaignBackupAsync(
        string campaignId,
        string targetDirectory,
        bool includeRetainedAudio,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        var resolvedTargetDir = string.IsNullOrWhiteSpace(targetDirectory)
            ? _config.GetBackupsDirectoryPath()
            : Path.GetFullPath(targetDirectory);

        Directory.CreateDirectory(resolvedTargetDir);

        var nowUtc = DateTimeOffset.UtcNow;
        var safeCampaignId = RavenEmbeddedConfiguration.SanitizeId(campaignId);
        var backupFileName = $"{safeCampaignId}_{nowUtc:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}.palantyr-backup";
        var backupFilePath = Path.Combine(resolvedTargetDir, backupFileName);

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var campaign = await LoadCampaignMetadataInternalAsync(campaignId, cancellationToken).ConfigureAwait(false);
            var sessions = await LoadSessionsInternalAsync(campaignId, cancellationToken).ConfigureAwait(false);
            var entities = await LoadEntitiesInternalAsync(campaignId, cancellationToken).ConfigureAwait(false);
            var transcripts = await LoadTranscriptsInternalAsync(campaignId, cancellationToken).ConfigureAwait(false);
            var needsAttention = await LoadNeedsAttentionItemsInternalAsync(campaignId, cancellationToken).ConfigureAwait(false);

            var dumpPayload = new CampaignBackupDataPayload(
                DatabaseName: RavenEmbeddedConfiguration.GetCampaignDatabaseName(campaignId),
                ExportedAtUtc: nowUtc,
                Campaign: campaign,
                Sessions: sessions,
                MemoryEntities: entities,
                Transcripts: transcripts,
                NeedsAttentionItems: needsAttention);

            var campaignDataJsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dumpPayload, JsonOptions));
            var checksums = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var audioFilesToInclude = new List<(string ArchiveEntryPath, string SourceFilePath)>();
            if (includeRetainedAudio)
            {
                var discoveredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // 1. Discover .wav files in the campaign directory
                var campaignDir = _config.GetCampaignDirectoryPath(campaignId);
                if (Directory.Exists(campaignDir))
                {
                    foreach (var wavFile in Directory.EnumerateFiles(campaignDir, "*.wav", SearchOption.AllDirectories))
                    {
                        if (discoveredPaths.Add(Path.GetFullPath(wavFile)))
                            {
                                var fileName = Path.GetFileName(wavFile);
                                audioFilesToInclude.Add(($"audio/{fileName}", wavFile));
                            }
                    }
                }

                // 2. Discover .wav files referenced by session WAL segments
                foreach (var session in sessions)
                {
                    foreach (var segment in session.Segments)
                    {
                        if (!string.IsNullOrWhiteSpace(segment.FilePath) &&
                            segment.FilePath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) &&
                            File.Exists(segment.FilePath) &&
                            discoveredPaths.Add(Path.GetFullPath(segment.FilePath)))
                        {
                            var fileName = Path.GetFileName(segment.FilePath);
                            var entryName = $"audio/{RavenEmbeddedConfiguration.SanitizeId(session.Id)}_{fileName}";
                            audioFilesToInclude.Add((entryName, segment.FilePath));
                        }
                    }
                }
            }

            if (File.Exists(backupFilePath))
            {
                File.Delete(backupFilePath);
            }

            using (var fileStream = new FileStream(backupFilePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: false))
            {
                // Write campaign_data.json
                await WriteArchiveEntryAsync(archive, "campaign_data.json", campaignDataJsonBytes, cancellationToken).ConfigureAwait(false);
                checksums["campaign_data.json"] = ComputeSha256Hex(campaignDataJsonBytes);

                // Write campaign.ravendbdump
                await WriteArchiveEntryAsync(archive, "campaign.ravendbdump", campaignDataJsonBytes, cancellationToken).ConfigureAwait(false);
                checksums["campaign.ravendbdump"] = ComputeSha256Hex(campaignDataJsonBytes);

                // Write retained .wav audio ONLY when includeRetainedAudio == true (AC-DATA-007)
                if (includeRetainedAudio)
                {
                    foreach (var (archiveEntryPath, sourceFilePath) in audioFilesToInclude)
                    {
                        var wavBytes = await File.ReadAllBytesAsync(sourceFilePath, cancellationToken).ConfigureAwait(false);
                        await WriteArchiveEntryAsync(archive, archiveEntryPath, wavBytes, cancellationToken).ConfigureAwait(false);
                        checksums[archiveEntryPath] = ComputeSha256Hex(wavBytes);
                    }
                }

                // Write manifest.json with SHA-256 checksums, IncludesRetainedAudio flag, and NFR-PRIV-007 warning
                var manifest = new CampaignBackupManifest(
                    SchemaVersion: "1.0",
                    ApplicationVersion: "1.0.0",
                    CampaignId: campaignId,
                    DatabaseName: RavenEmbeddedConfiguration.GetCampaignDatabaseName(campaignId),
                    CreatedAtUtc: nowUtc,
                    IncludesRetainedAudio: includeRetainedAudio,
                    RetainedAudioFileCount: includeRetainedAudio ? audioFilesToInclude.Count : 0,
                    SessionCount: sessions.Count,
                    MemoryEntityCount: entities.Count,
                    PrivacyNoticeEn: BackupPrivacyNoticeEn,
                    PrivacyNoticeEs: BackupPrivacyNoticeEs,
                    ChecksumsSha256: checksums);

                var manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest, JsonOptions));
                await WriteArchiveEntryAsync(archive, "manifest.json", manifestBytes, cancellationToken).ConfigureAwait(false);
            }

            await UpdateCampaignLastBackupTimestampInternalAsync(campaignId, nowUtc, cancellationToken).ConfigureAwait(false);
            return backupFilePath;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private string GetSessionsFilePath(string campaignId) =>
        Path.Combine(_config.GetCampaignDirectoryPath(campaignId), "Sessions", "sessions.json");

    private string GetEntitiesFilePath(string campaignId) =>
        Path.Combine(_config.GetCampaignDirectoryPath(campaignId), "Memory", "memory_entities.json");

    private string GetTranscriptsFilePath(string campaignId) =>
        Path.Combine(_config.GetCampaignDirectoryPath(campaignId), "Transcripts", "transcripts.json");

    private string GetNeedsAttentionFilePath(string campaignId) =>
        Path.Combine(_config.GetCampaignDirectoryPath(campaignId), "Review", "needs_attention.json");

    private string GetResolutionCandidatesFilePath(string campaignId) =>
        Path.Combine(_config.GetCampaignDirectoryPath(campaignId), "Memory", "resolution_candidates.json");

    private string GetConflictsFilePath(string campaignId) =>
        Path.Combine(_config.GetCampaignDirectoryPath(campaignId), "Memory", "memory_conflicts.json");

    private async Task<List<EntityResolutionCandidateDto>> LoadResolutionCandidatesInternalAsync(string campaignId, CancellationToken cancellationToken)
    {
        var path = GetResolutionCandidatesFilePath(campaignId);
        if (!File.Exists(path))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<List<EntityResolutionCandidateDto>>(json, JsonOptions) ?? [];
    }

    private async Task SaveResolutionCandidatesInternalAsync(string campaignId, List<EntityResolutionCandidateDto> items, CancellationToken cancellationToken)
    {
        var path = GetResolutionCandidatesFilePath(campaignId);
        var json = JsonSerializer.Serialize(items, JsonOptions);
        await RavenSystemStore.WriteFileAtomicallyAsync(path, json, cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<MemoryConflictDto>> LoadConflictsInternalAsync(string campaignId, CancellationToken cancellationToken)
    {
        var path = GetConflictsFilePath(campaignId);
        if (!File.Exists(path))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<List<MemoryConflictDto>>(json, JsonOptions) ?? [];
    }

    private async Task SaveConflictsInternalAsync(string campaignId, List<MemoryConflictDto> items, CancellationToken cancellationToken)
    {
        var path = GetConflictsFilePath(campaignId);
        var json = JsonSerializer.Serialize(items, JsonOptions);
        await RavenSystemStore.WriteFileAtomicallyAsync(path, json, cancellationToken).ConfigureAwait(false);
    }


    private string GetCampaignSnapshotFilePath(string campaignId) =>
        Path.Combine(_config.GetCampaignDirectoryPath(campaignId), "campaign.json");

    private string GetSystemCampaignsCatalogFilePath() =>
        Path.Combine(_config.GetSystemDirectoryPath(), "campaigns.json");

    private async Task<List<SessionDocument>> LoadSessionsInternalAsync(string campaignId, CancellationToken cancellationToken)
    {
        var path = GetSessionsFilePath(campaignId);
        if (!File.Exists(path))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<List<SessionDocument>>(json, JsonOptions) ?? [];
    }

    private async Task SaveSessionsInternalAsync(string campaignId, List<SessionDocument> sessions, CancellationToken cancellationToken)
    {
        var path = GetSessionsFilePath(campaignId);
        var json = JsonSerializer.Serialize(sessions, JsonOptions);
        await RavenSystemStore.WriteFileAtomicallyAsync(path, json, cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<MemoryEntityDocument>> LoadEntitiesInternalAsync(string campaignId, CancellationToken cancellationToken)
    {
        var path = GetEntitiesFilePath(campaignId);
        if (!File.Exists(path))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<List<MemoryEntityDocument>>(json, JsonOptions) ?? [];
    }

    private async Task SaveEntitiesInternalAsync(string campaignId, List<MemoryEntityDocument> entities, CancellationToken cancellationToken)
    {
        var path = GetEntitiesFilePath(campaignId);
        var json = JsonSerializer.Serialize(entities, JsonOptions);
        await RavenSystemStore.WriteFileAtomicallyAsync(path, json, cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<TranscriptSegmentDocument>> LoadTranscriptsInternalAsync(string campaignId, CancellationToken cancellationToken)
    {
        var path = GetTranscriptsFilePath(campaignId);
        if (!File.Exists(path))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<List<TranscriptSegmentDocument>>(json, JsonOptions) ?? [];
    }

    private async Task SaveTranscriptsInternalAsync(string campaignId, List<TranscriptSegmentDocument> transcripts, CancellationToken cancellationToken)
    {
        var path = GetTranscriptsFilePath(campaignId);
        var json = JsonSerializer.Serialize(transcripts, JsonOptions);
        await RavenSystemStore.WriteFileAtomicallyAsync(path, json, cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<NeedsAttentionItem>> LoadNeedsAttentionItemsInternalAsync(string campaignId, CancellationToken cancellationToken)
    {
        var path = GetNeedsAttentionFilePath(campaignId);
        if (!File.Exists(path))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<List<NeedsAttentionItem>>(json, JsonOptions) ?? [];
    }

    private async Task SaveNeedsAttentionItemsInternalAsync(string campaignId, List<NeedsAttentionItem> items, CancellationToken cancellationToken)
    {
        var path = GetNeedsAttentionFilePath(campaignId);
        var json = JsonSerializer.Serialize(items, JsonOptions);
        await RavenSystemStore.WriteFileAtomicallyAsync(path, json, cancellationToken).ConfigureAwait(false);
    }

    private async Task AppendDocumentRevisionInternalAsync(
        string campaignId,
        MemoryEntityDocument entity,
        CancellationToken cancellationToken)
    {
        var revisionsDir = Path.Combine(_config.GetCampaignDirectoryPath(campaignId), "Revisions");
        Directory.CreateDirectory(revisionsDir);
        var safeEntityId = RavenEmbeddedConfiguration.SanitizeId(entity.Id);
        var revisionPath = Path.Combine(
            revisionsDir,
            $"{safeEntityId}_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid().ToString("N")[..6]}.rev.json");
        var json = JsonSerializer.Serialize(entity, JsonOptions);
        await File.WriteAllTextAsync(revisionPath, json, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CampaignDocument?> LoadCampaignMetadataInternalAsync(string campaignId, CancellationToken cancellationToken)
    {
        var snapshotPath = GetCampaignSnapshotFilePath(campaignId);
        if (File.Exists(snapshotPath))
        {
            var json = await File.ReadAllTextAsync(snapshotPath, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<CampaignDocument>(json, JsonOptions);
        }

        var catalogPath = GetSystemCampaignsCatalogFilePath();
        if (File.Exists(catalogPath))
        {
            var json = await File.ReadAllTextAsync(catalogPath, cancellationToken).ConfigureAwait(false);
            var campaigns = JsonSerializer.Deserialize<List<CampaignDocument>>(json, JsonOptions) ?? [];
            return campaigns.FirstOrDefault(c => string.Equals(c.Id, campaignId, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private async Task TouchCampaignMeaningfulChangeInternalAsync(
        string campaignId,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken)
    {
        var snapshotPath = GetCampaignSnapshotFilePath(campaignId);
        if (File.Exists(snapshotPath))
        {
            var snapshotJson = await File.ReadAllTextAsync(snapshotPath, cancellationToken).ConfigureAwait(false);
            var snapshot = JsonSerializer.Deserialize<CampaignDocument>(snapshotJson, JsonOptions);
            if (snapshot is not null)
            {
                snapshot.LastMeaningfulChangeAtUtc = changedAtUtc;
                await RavenSystemStore.WriteFileAtomicallyAsync(
                    snapshotPath,
                    JsonSerializer.Serialize(snapshot, JsonOptions),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        var catalogPath = GetSystemCampaignsCatalogFilePath();
        if (File.Exists(catalogPath))
        {
            var catalogJson = await File.ReadAllTextAsync(catalogPath, cancellationToken).ConfigureAwait(false);
            var campaigns = JsonSerializer.Deserialize<List<CampaignDocument>>(catalogJson, JsonOptions);
            if (campaigns is not null)
            {
                var match = campaigns.FirstOrDefault(c => string.Equals(c.Id, campaignId, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                {
                    match.LastMeaningfulChangeAtUtc = changedAtUtc;
                    await RavenSystemStore.WriteFileAtomicallyAsync(
                        catalogPath,
                        JsonSerializer.Serialize(campaigns, JsonOptions),
                        cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task UpdateCampaignLastBackupTimestampInternalAsync(
        string campaignId,
        DateTimeOffset backupAtUtc,
        CancellationToken cancellationToken)
    {
        var snapshotPath = GetCampaignSnapshotFilePath(campaignId);
        if (File.Exists(snapshotPath))
        {
            var snapshotJson = await File.ReadAllTextAsync(snapshotPath, cancellationToken).ConfigureAwait(false);
            var snapshot = JsonSerializer.Deserialize<CampaignDocument>(snapshotJson, JsonOptions);
            if (snapshot is not null)
            {
                snapshot.LastBackupAtUtc = backupAtUtc;
                await RavenSystemStore.WriteFileAtomicallyAsync(
                    snapshotPath,
                    JsonSerializer.Serialize(snapshot, JsonOptions),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        var catalogPath = GetSystemCampaignsCatalogFilePath();
        if (File.Exists(catalogPath))
        {
            var catalogJson = await File.ReadAllTextAsync(catalogPath, cancellationToken).ConfigureAwait(false);
            var campaigns = JsonSerializer.Deserialize<List<CampaignDocument>>(catalogJson, JsonOptions);
            if (campaigns is not null)
            {
                var match = campaigns.FirstOrDefault(c => string.Equals(c.Id, campaignId, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                {
                    match.LastBackupAtUtc = backupAtUtc;
                    await RavenSystemStore.WriteFileAtomicallyAsync(
                        catalogPath,
                        JsonSerializer.Serialize(campaigns, JsonOptions),
                        cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private static async Task WriteArchiveEntryAsync(
        ZipArchive archive,
        string entryPath,
        byte[] content,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
        await using var entryStream = entry.Open();
        await entryStream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
    }

    private static string ComputeSha256Hex(byte[] data)
    {
        var hashBytes = SHA256.HashData(data);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

/// <summary>
/// Manifest stored inside `.palantyr-backup` archives (ADR-009, AC-DATA-003, AC-DATA-007, NFR-PRIV-007).
/// </summary>
public sealed record CampaignBackupManifest(
    string SchemaVersion,
    string ApplicationVersion,
    string CampaignId,
    string DatabaseName,
    DateTimeOffset CreatedAtUtc,
    bool IncludesRetainedAudio,
    int RetainedAudioFileCount,
    int SessionCount,
    int MemoryEntityCount,
    string PrivacyNoticeEn,
    string PrivacyNoticeEs,
    IReadOnlyDictionary<string, string> ChecksumsSha256);

/// <summary>
/// Serialized campaign database payload stored in `campaign_data.json` / `campaign.ravendbdump` inside `.palantyr-backup`.
/// </summary>
public sealed record CampaignBackupDataPayload(
    string DatabaseName,
    DateTimeOffset ExportedAtUtc,
    CampaignDocument? Campaign,
    IReadOnlyList<SessionDocument> Sessions,
    IReadOnlyList<MemoryEntityDocument> MemoryEntities,
    IReadOnlyList<TranscriptSegmentDocument>? Transcripts = null,
    IReadOnlyList<NeedsAttentionItem>? NeedsAttentionItems = null);