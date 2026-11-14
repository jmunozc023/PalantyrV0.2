using Palantyr.Contracts;

namespace Palantyr.AI.Orchestration;

/// <summary>
/// Enforces runtime priority tiers (1. Recording > 2. Eonix Query > 3. Background Processing),
/// the Single-Active-GPU-Worker Mutex on &lt; 12 GB VRAM systems (Kaede Finding 2),
/// and adaptive resource throttling with bilingual dragon + technical explanations (FR-PERF-001, NFR-PERF-002..008).
/// </summary>
public sealed class RuntimePriorityScheduler : IRuntimePriorityScheduler, IDisposable
{
    public const double MaxAllowedCpuPercent = 80.0;
    public const double MinRequiredAvailableRamGb = 2.0;
    public const double MinRequiredFreeStorageGb = 2.0;
    public const double SingleGpuMutexVramThresholdGb = 12.0;

    private readonly SemaphoreSlim _gpuWorkerMutex = new(1, 1);
    private readonly Lock _stateLock = new();
    private readonly double _detectedVramGb;

    private SessionRecordingState _currentSessionState = SessionRecordingState.Idle;
    private bool _isBackgroundThrottled;
    private string? _throttlingReasonEn;
    private string? _throttlingReasonEs;
    private int _activeGpuWorkerCount;

    public RuntimePriorityScheduler(double detectedVramGb = 8.0)
    {
        _detectedVramGb = detectedVramGb;
    }

    public SessionRecordingState CurrentSessionState
    {
        get
        {
            lock (_stateLock)
            {
                return _currentSessionState;
            }
        }
    }

    public int ActiveGpuWorkerCount => Volatile.Read(ref _activeGpuWorkerCount);

    public bool EnforceSingleGpuWorkerMutex => _detectedVramGb < SingleGpuMutexVramThresholdGb;

    public bool IsBackgroundThrottled
    {
        get
        {
            lock (_stateLock)
            {
                return _isBackgroundThrottled;
            }
        }
    }

    public string? ThrottlingReasonEn
    {
        get
        {
            lock (_stateLock)
            {
                return _throttlingReasonEn;
            }
        }
    }

    public string? ThrottlingReasonEs
    {
        get
        {
            lock (_stateLock)
            {
                return _throttlingReasonEs;
            }
        }
    }

    public void NotifySessionStateChanged(SessionRecordingState newState)
    {
        lock (_stateLock)
        {
            _currentSessionState = newState;
            if (newState is SessionRecordingState.Idle or SessionRecordingState.Ended)
            {
                _isBackgroundThrottled = false;
                _throttlingReasonEn = null;
                _throttlingReasonEs = null;
            }
        }
    }

    public async Task<IDisposable> AcquireGpuWorkerSlotAsync(
        RuntimePriorityTier priority,
        CancellationToken cancellationToken = default)
    {
        await _gpuWorkerMutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _activeGpuWorkerCount);
        return new GpuWorkerSlotLease(this);
    }

    public bool ShouldDeferBackgroundWork(
        double currentCpuPercent,
        double availableRamGb,
        double freeStorageGb)
    {
        var reasonsEn = new List<string>();
        var reasonsEs = new List<string>();

        if (currentCpuPercent > MaxAllowedCpuPercent)
        {
            reasonsEn.Add($"CPU load is at {currentCpuPercent:F1}% (> {MaxAllowedCpuPercent:F0}% threshold)");
            reasonsEs.Add($"la carga de CPU está al {currentCpuPercent:F1}% (> umbral del {MaxAllowedCpuPercent:F0}%)");
        }

        if (availableRamGb < MinRequiredAvailableRamGb)
        {
            reasonsEn.Add($"available RAM is {availableRamGb:F2} GB (< {MinRequiredAvailableRamGb:F1} GB minimum)");
            reasonsEs.Add($"la RAM disponible es {availableRamGb:F2} GB (< mínimo de {MinRequiredAvailableRamGb:F1} GB)");
        }

        if (freeStorageGb < MinRequiredFreeStorageGb)
        {
            reasonsEn.Add($"free disk space is {freeStorageGb:F2} GB (< {MinRequiredFreeStorageGb:F1} GB minimum)");
            reasonsEs.Add($"el espacio libre en disco es {freeStorageGb:F2} GB (< mínimo de {MinRequiredFreeStorageGb:F1} GB)");
        }

        lock (_stateLock)
        {
            if (reasonsEn.Count > 0)
            {
                _isBackgroundThrottled = true;
                string joinedEn = string.Join("; ", reasonsEn);
                string joinedEs = string.Join("; ", reasonsEs);

                _throttlingReasonEn =
                    $"Eonix folded his wings to guard the table's voice — background transcription and memory indexing are paused while {joinedEn}. Live session recording continues safely.";
                _throttlingReasonEs =
                    $"Eonix plegó sus alas para proteger la voz de la mesa — la transcripción en segundo plano y el indexado de memoria están en pausa mientras {joinedEs}. La grabación de la sesión continúa de forma segura.";

                return true;
            }

            _isBackgroundThrottled = false;
            _throttlingReasonEn = null;
            _throttlingReasonEs = null;
            return false;
        }
    }

    private void ReleaseGpuSlot()
    {
        Interlocked.Decrement(ref _activeGpuWorkerCount);
        _gpuWorkerMutex.Release();
    }

    public void Dispose()
    {
        _gpuWorkerMutex.Dispose();
    }

    private sealed class GpuWorkerSlotLease : IDisposable
    {
        private RuntimePriorityScheduler? _owner;

        public GpuWorkerSlotLease(RuntimePriorityScheduler owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?.ReleaseGpuSlot();
        }
    }
}
