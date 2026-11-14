namespace Palantyr.Data.Raven;

/// <summary>
/// Encapsulates ADR-002 and Kaede Finding 2 resource guardrails for RavenDB Embedded
/// coexistence alongside 3D Virtual Tabletops (VTTs) and voice chat during active sessions.
/// Strictly binds to local loopback 127.0.0.1 (NFR-PRIV-005).
/// </summary>
public sealed class RavenEmbeddedConfiguration
{
    public const string SystemDatabaseName = "Palantyr_System";

    public const int DefaultMaxScratchBufferSizeInMb = 64;
    public const int DefaultLowMemoryCommitLimitInMb = 256;
    public const int DefaultMaxNumberOfConcurrentlyRunningIndexes = 1;
    public const int DefaultMaxIdleTimeForTenantDatabaseInSec = 900;
    public const string DefaultBindAddress = "127.0.0.1";

    public RavenEmbeddedConfiguration()
    {
    }

    public RavenEmbeddedConfiguration(string baseDataDirectory)
    {
        if (!string.IsNullOrWhiteSpace(baseDataDirectory))
        {
            BaseDataDirectory = Path.GetFullPath(baseDataDirectory);
        }
    }

    public static RavenEmbeddedConfiguration Default { get; } = new();

    /// <summary>
    /// Root local storage path for system and per-campaign databases.
    /// </summary>
    public string BaseDataDirectory { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Palantyr");

    /// <summary>
    /// Voron storage scratch buffer cap in MB (ADR-002, Kaede Finding 2: 64 MB).
    /// </summary>
    public int MaxScratchBufferSizeInMb { get; init; } = DefaultMaxScratchBufferSizeInMb;

    /// <summary>
    /// Low-memory commit threshold in MB (ADR-002, Kaede Finding 2: 256 MB).
    /// </summary>
    public int LowMemoryCommitLimitInMb { get; init; } = DefaultLowMemoryCommitLimitInMb;

    /// <summary>
    /// Maximum concurrent Corax/HNSW indexing threads during desktop play (ADR-002, Kaede Finding 2: 1).
    /// </summary>
    public int MaxNumberOfConcurrentlyRunningIndexes { get; init; } = DefaultMaxNumberOfConcurrentlyRunningIndexes;

    /// <summary>
    /// Idle timeout before unloading inactive campaign tenant databases (ADR-002, Kaede Finding 2: 900s).
    /// </summary>
    public int MaxIdleTimeForTenantDatabaseInSec { get; init; } = DefaultMaxIdleTimeForTenantDatabaseInSec;

    /// <summary>
    /// Strict loopback bind address so RavenDB Embedded never exposes network ports (NFR-PRIV-005).
    /// </summary>
    public string BindAddress { get; init; } = DefaultBindAddress;

    /// <summary>
    /// Canonical per-campaign database name helper (ADR-002).
    /// </summary>
    public static string GetCampaignDatabaseName(string campaignId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        return $"Palantyr_Campaign_{campaignId.Trim()}";
    }

    public string GetSystemDirectoryPath() => Path.Combine(BaseDataDirectory, "System");

    public string GetCampaignsRootDirectoryPath() => Path.Combine(BaseDataDirectory, "Campaigns");

    public string GetCampaignDirectoryPath(string campaignId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        return Path.Combine(GetCampaignsRootDirectoryPath(), SanitizeId(campaignId));
    }

    public string GetBackupsDirectoryPath() => Path.Combine(BaseDataDirectory, "Backups");

    /// <summary>
    /// Produces the RavenDB Embedded server command-line arguments enforcing ADR-002 and Kaede Finding 2.
    /// </summary>
    public IReadOnlyList<string> BuildServerCommandLineArgs()
    {
        ValidateGuardrails();

        return
        [
            $"--ServerUrl=http://{BindAddress}:0",
            "--Security.UnsecuredAccessAllowed=Local",
            $"--Storage.MaxScratchBufferSizeInMb={MaxScratchBufferSizeInMb}",
            $"--Memory.LowMemoryCommitLimitInMb={LowMemoryCommitLimitInMb}",
            $"--Indexing.MaxNumberOfConcurrentlyRunningIndexes={MaxNumberOfConcurrentlyRunningIndexes}",
            $"--Databases.MaxIdleTimeForTenantDatabaseInSec={MaxIdleTimeForTenantDatabaseInSec}",
            "--License.Eula.Accepted=true"
        ];
    }

    /// <summary>
    /// Validates that privacy and resource guardrails are within safe bounds.
    /// </summary>
    public void ValidateGuardrails()
    {
        if (!string.Equals(BindAddress, "127.0.0.1", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(BindAddress, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"NFR-PRIV-005 violation: RavenDB Embedded must bind strictly to local loopback (127.0.0.1), not '{BindAddress}'.");
        }

        if (MaxScratchBufferSizeInMb <= 0 || MaxScratchBufferSizeInMb > 256)
        {
            throw new InvalidOperationException(
                $"ADR-002 guardrail violation: MaxScratchBufferSizeInMb ({MaxScratchBufferSizeInMb}) must be between 1 and 256 MB.");
        }

        if (LowMemoryCommitLimitInMb <= 0)
        {
            throw new InvalidOperationException("ADR-002 guardrail violation: LowMemoryCommitLimitInMb must be positive.");
        }

        if (MaxNumberOfConcurrentlyRunningIndexes < 1)
        {
            throw new InvalidOperationException("ADR-002 guardrail violation: MaxNumberOfConcurrentlyRunningIndexes must be at least 1.");
        }
    }

    internal static string SanitizeId(string id)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = id.Trim().Select(c => invalidChars.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }
}
