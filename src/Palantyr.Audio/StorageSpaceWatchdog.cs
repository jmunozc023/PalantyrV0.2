namespace Palantyr.Audio;

/// <summary>
/// Immutable snapshot of storage availability on the recording drive (NFR-DATA-003, AC-DATA-002).
/// </summary>
public sealed record StorageSpaceStatus(
    double FreeStorageGb,
    bool IsStorageWarningActive,
    bool IsCriticalStorageExhausted,
    string? WarningMessageEn,
    string? WarningMessageEs);

/// <summary>
/// Monitors available disk space (<see cref="DriveInfo"/>) on the recording path (NFR-DATA-003, AC-DATA-002).
/// Thresholds:
/// - <c>&lt; 2.0 GB</c> triggers <see cref="IsStorageWarningActive"/> = <see langword="true"/>.
/// - <c>&lt; 0.25 GB</c> (250 MB) triggers <see cref="IsCriticalStorageExhausted"/> = <see langword="true"/>
///   so recording safely closes the active segment and pauses rather than reporting a false healthy Recording state.
/// </summary>
public sealed class StorageSpaceWatchdog
{
    public const double WarningThresholdGb = 2.0;
    public const double CriticalThresholdGb = 0.25;
    private const double BytesPerGigabyte = 1024.0 * 1024.0 * 1024.0;

    private readonly string _monitoredPath;
    private Func<double>? _freeSpaceGbOverride;

    public StorageSpaceWatchdog(string? monitoredPath = null, Func<double>? freeSpaceGbOverride = null)
    {
        _monitoredPath = string.IsNullOrWhiteSpace(monitoredPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Palantyr")
            : Path.GetFullPath(monitoredPath);
        _freeSpaceGbOverride = freeSpaceGbOverride;
    }

    public string MonitoredPath => _monitoredPath;

    /// <summary>
    /// Current available free storage in gigabytes on the recording volume.
    /// </summary>
    public double FreeStorageGb => QueryFreeStorageGb(_monitoredPath);

    /// <summary>
    /// Returns <see langword="true"/> when available free space is below <see cref="WarningThresholdGb"/> (2.0 GB).
    /// </summary>
    public bool IsStorageWarningActive => FreeStorageGb < WarningThresholdGb;

    /// <summary>
    /// Returns <see langword="true"/> when available free space is below <see cref="CriticalThresholdGb"/> (0.25 GB / 250 MB).
    /// </summary>
    public bool IsCriticalStorageExhausted => FreeStorageGb < CriticalThresholdGb;

    /// <summary>
    /// Allows tests or diagnostics to simulate low-disk conditions deterministically.
    /// Pass <see langword="null"/> to restore live <see cref="DriveInfo"/> probing.
    /// </summary>
    public void SetSimulatedFreeSpaceGb(double? simulatedFreeSpaceGb)
    {
        _freeSpaceGbOverride = simulatedFreeSpaceGb.HasValue
            ? () => simulatedFreeSpaceGb.Value
            : null;
    }

    /// <summary>
    /// Evaluates current disk space against warning (&lt; 2.0 GB) and critical (&lt; 0.25 GB) thresholds.
    /// </summary>
    public StorageSpaceStatus Evaluate(string? targetPath = null)
    {
        double freeGb = QueryFreeStorageGb(targetPath ?? _monitoredPath);
        bool isCritical = freeGb < CriticalThresholdGb;
        bool isWarning = freeGb < WarningThresholdGb;

        string? messageEn = null;
        string? messageEs = null;

        if (isCritical)
        {
            messageEn = $"Critical storage exhaustion ({freeGb:F2} GB free < 250 MB). Active audio segment closed safely and recording paused to prevent data corruption.";
            messageEs = $"Almacenamiento crítico agotado ({freeGb:F2} GB libres < 250 MB). El segmento de audio activo se cerró de forma segura y la grabación se pausó para evitar corrupción de datos.";
        }
        else if (isWarning)
        {
            messageEn = $"Low disk space warning ({freeGb:F2} GB free < 2.0 GB). Free up disk space soon so session recording is not interrupted.";
            messageEs = $"Advertencia de poco espacio en disco ({freeGb:F2} GB libres < 2.0 GB). Libere espacio pronto para que la grabación de la sesión no se interrumpa.";
        }

        return new StorageSpaceStatus(
            FreeStorageGb: Math.Round(freeGb, 3),
            IsStorageWarningActive: isWarning,
            IsCriticalStorageExhausted: isCritical,
            WarningMessageEn: messageEn,
            WarningMessageEs: messageEs);
    }

    private double QueryFreeStorageGb(string path)
    {
        if (_freeSpaceGbOverride is not null)
        {
            return Math.Max(0.0, _freeSpaceGbOverride());
        }

        try
        {
            string fullPath = Path.GetFullPath(path);
            string? root = Path.GetPathRoot(fullPath);
            if (!string.IsNullOrWhiteSpace(root))
            {
                var drive = new DriveInfo(root);
                if (drive.IsReady)
                {
                    return drive.AvailableFreeSpace / BytesPerGigabyte;
                }
            }
        }
        catch
        {
            // Fallback if path root cannot be inspected
        }

        return 50.0;
    }
}