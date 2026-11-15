using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Palantyr.Contracts;

namespace Palantyr.Audio;

/// <summary>
/// Manages the authoritative <c>session_recording.pseg</c> JSON Lines Write-Ahead Log on disk
/// with <see cref="FileOptions.WriteThrough"/> atomic flushing (ADR-003, Kaede Finding 1).
/// </summary>
public sealed class PsegWalManager
{
    public const string WalFileName = "session_recording.pseg";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false
    };

    private readonly string _baseStorageDirectory;
    private readonly SemaphoreSlim _walLock = new(1, 1);

    public PsegWalManager(string? baseStorageDirectory = null)
    {
        _baseStorageDirectory = string.IsNullOrWhiteSpace(baseStorageDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Palantyr")
            : Path.GetFullPath(baseStorageDirectory);
    }

    public string BaseStorageDirectory => _baseStorageDirectory;

    public string GetCampaignDirectory(string campaignId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        return Path.Combine(_baseStorageDirectory, "Campaigns", SanitizePathSegment(campaignId));
    }

    public string GetSessionsRootDirectory(string campaignId)
    {
        return Path.Combine(GetCampaignDirectory(campaignId), "Sessions");
    }

    public string GetSessionAudioDirectory(string campaignId, string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return Path.Combine(GetSessionsRootDirectory(campaignId), SanitizePathSegment(sessionId), "Audio");
    }

    public string GetWalFilePath(string campaignId, string sessionId)
    {
        return Path.Combine(GetSessionAudioDirectory(campaignId, sessionId), WalFileName);
    }

    public string GetSegmentFilePath(string campaignId, string sessionId, int sequenceNumber)
    {
        return Path.Combine(GetSessionAudioDirectory(campaignId, sessionId), $"segment_{sequenceNumber:D3}.wav");
    }

    /// <summary>
    /// Appends or updates an <see cref="AudioSegmentWalEntry"/> in the session's <c>session_recording.pseg</c>
    /// using <see cref="FileOptions.WriteThrough"/>.
    /// </summary>
    public Task AppendOrUpdateEntryAsync(
        string campaignId,
        string sessionId,
        AudioSegmentWalEntry entry,
        CancellationToken cancellationToken = default)
    {
        string walPath = GetWalFilePath(campaignId, sessionId);
        return AppendOrUpdateEntryToFileAsync(walPath, entry, cancellationToken);
    }

    /// <summary>
    /// Appends or updates an <see cref="AudioSegmentWalEntry"/> directly to the specified <paramref name="walFilePath"/>
    /// using <see cref="FileOptions.WriteThrough"/> and an explicit disk flush.
    /// </summary>
    public async Task AppendOrUpdateEntryToFileAsync(
        string walFilePath,
        AudioSegmentWalEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(walFilePath);
        ArgumentNullException.ThrowIfNull(entry);

        await _walLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string? directory = Path.GetDirectoryName(walFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string jsonLine = JsonSerializer.Serialize(entry, JsonOptions) + "\n";
            byte[] payload = Encoding.UTF8.GetBytes(jsonLine);

            await using var stream = new FileStream(
                walFilePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 4096,
                options: FileOptions.WriteThrough | FileOptions.Asynchronous);

            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            stream.Flush(flushToDisk: true);
        }
        finally
        {
            _walLock.Release();
        }
    }

    /// <summary>
    /// Reads all latest segment states for a session from its <c>session_recording.pseg</c> WAL,
    /// ordered by <see cref="AudioSegmentWalEntry.SequenceNumber"/>.
    /// </summary>
    public Task<IReadOnlyList<AudioSegmentWalEntry>> ReadEntriesAsync(
        string campaignId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        string walPath = GetWalFilePath(campaignId, sessionId);
        return ReadEntriesFromFileAsync(walPath, cancellationToken);
    }

    /// <summary>
    /// Reads all latest segment states from the specified <paramref name="walFilePath"/>,
    /// collapsing multiple JSONL log lines for the same <see cref="AudioSegmentWalEntry.SegmentId"/>
    /// to the most recently appended state.
    /// </summary>
    public async Task<IReadOnlyList<AudioSegmentWalEntry>> ReadEntriesFromFileAsync(
        string walFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(walFilePath);

        if (!File.Exists(walFilePath))
        {
            return [];
        }

        await _walLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var latestBySegmentId = new Dictionary<string, AudioSegmentWalEntry>(StringComparer.OrdinalIgnoreCase);

            await using var stream = new FileStream(
                walFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: 4096,
                options: FileOptions.SequentialScan | FileOptions.Asynchronous);

            using var reader = new StreamReader(stream, Encoding.UTF8);
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var entry = JsonSerializer.Deserialize<AudioSegmentWalEntry>(line, JsonOptions);
                    if (entry is not null && !string.IsNullOrWhiteSpace(entry.SegmentId))
                    {
                        latestBySegmentId[entry.SegmentId] = entry;
                    }
                }
                catch (JsonException)
                {
                    // Ignore partially written trailing line caused by abrupt power loss
                }
            }

            return latestBySegmentId.Values
                .OrderBy(e => e.SequenceNumber)
                .ThenBy(e => e.StartedAtUtc)
                .ToList();
        }
        finally
        {
            _walLock.Release();
        }
    }

    /// <summary>
    /// Scans all session directories inside a campaign for <c>session_recording.pseg</c> files
    /// that contain at least one unclosed <see cref="AudioSegmentWalStatus.Active"/> segment (FR-REC-006).
    /// </summary>
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<AudioSegmentWalEntry>>> ScanCampaignForUnclosedSessionsAsync(
        string campaignId,
        CancellationToken cancellationToken = default)
    {
        var allSessions = await ScanAllCampaignSessionsAsync(campaignId, cancellationToken).ConfigureAwait(false);
        var unclosed = new Dictionary<string, IReadOnlyList<AudioSegmentWalEntry>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (sessionId, entries) in allSessions)
        {
            if (entries.Any(e => e.Status == AudioSegmentWalStatus.Active))
            {
                unclosed[sessionId] = entries;
            }
        }

        return unclosed;
    }

    /// <summary>
    /// Scans all session directories inside a campaign and returns every session's latest WAL entries.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<AudioSegmentWalEntry>>> ScanAllCampaignSessionsAsync(
        string campaignId,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, IReadOnlyList<AudioSegmentWalEntry>>(StringComparer.OrdinalIgnoreCase);
        string sessionsRoot = GetSessionsRootDirectory(campaignId);

        if (!Directory.Exists(sessionsRoot))
        {
            return results;
        }

        string[] walFiles = Directory.GetFiles(sessionsRoot, WalFileName, SearchOption.AllDirectories);
        foreach (string walFile in walFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entries = await ReadEntriesFromFileAsync(walFile, cancellationToken).ConfigureAwait(false);
            if (entries.Count == 0)
            {
                continue;
            }

            string sessionId = entries[0].SessionId;
            results[sessionId] = entries;
        }

        return results;
    }

    private static string SanitizePathSegment(string segment)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(segment.Length);
        foreach (char c in segment)
        {
            sb.Append(invalidChars.Contains(c) ? '_' : c);
        }
        return sb.ToString();
    }
}