using System.Collections.Concurrent;
using Palantyr.Contracts;
using Palantyr.Domain;

namespace Palantyr.Host;

/// <summary>
/// Explicit actions allowed when a user attempts to close the desktop window during active recording (NFR-REL-009, AC-REL-004).
/// </summary>
public enum WindowCloseResolutionAction
{
    ReturnToSession = 0,
    EndSessionAndExit = 1
}

/// <summary>
/// Result of evaluating a desktop window close attempt (NFR-REL-009, AC-REL-004).
/// </summary>
public sealed record WindowCloseCheckResult(
    bool CanExitImmediately,
    bool RequiresUserConfirmation,
    string? ActiveCampaignId,
    string? ActiveSessionId,
    string? WarningMessageEn,
    string? WarningMessageEs,
    IReadOnlyList<WindowCloseResolutionAction> AllowedActions);

/// <summary>
/// Guards the desktop window from silent exit while a session is actively recording (NFR-REL-009, AC-REL-004).
/// </summary>
public sealed class WindowCloseGuardService
{
    private readonly ConcurrentDictionary<string, (string CampaignId, string SessionId, SessionRecordingState State)> _trackedSessions =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Updates the tracked recording state for a campaign session.
    /// </summary>
    public void NotifySessionState(string campaignId, string sessionId, SessionRecordingState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        string key = $"{campaignId}::{sessionId}";
        if (state == SessionRecordingState.Recording)
        {
            _trackedSessions[key] = (campaignId, sessionId, state);
        }
        else
        {
            _trackedSessions.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Evaluates whether the application window can close immediately or must block and prompt the DM.
    /// </summary>
    public WindowCloseCheckResult EvaluateWindowClose()
    {
        var active = _trackedSessions.Values.FirstOrDefault(s => s.State == SessionRecordingState.Recording);
        if (active != default && !string.IsNullOrWhiteSpace(active.CampaignId))
        {
            return CreateBlockedResult(active.CampaignId, active.SessionId);
        }

        return new WindowCloseCheckResult(
            CanExitImmediately: true,
            RequiresUserConfirmation: false,
            ActiveCampaignId: null,
            ActiveSessionId: null,
            WarningMessageEn: null,
            WarningMessageEs: null,
            AllowedActions: []);
    }

    /// <summary>
    /// Evaluates window close by checking both in-memory tracked state and persisted campaign sessions.
    /// </summary>
    public async Task<WindowCloseCheckResult> EvaluateWindowCloseAsync(
        ISystemStore systemStore,
        ICampaignMemoryStore campaignMemoryStore,
        CancellationToken cancellationToken = default)
    {
        var immediate = EvaluateWindowClose();
        if (!immediate.CanExitImmediately)
        {
            return immediate;
        }

        var campaigns = await systemStore.ListCampaignsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var campaign in campaigns)
        {
            var sessions = await campaignMemoryStore.ListSessionsAsync(campaign.CampaignId, cancellationToken).ConfigureAwait(false);
            var activeRecording = sessions.FirstOrDefault(s => s.RecordingState == SessionRecordingState.Recording);
            if (activeRecording is not null)
            {
                NotifySessionState(campaign.CampaignId, activeRecording.Id, SessionRecordingState.Recording);
                return CreateBlockedResult(campaign.CampaignId, activeRecording.Id);
            }
        }

        return immediate;
    }

    /// <summary>
    /// Executes the DM's explicit decision from the window-close confirmation modal.
    /// If <see cref="WindowCloseResolutionAction.EndSessionAndExit"/> is selected, safely ends the active recording
    /// before permitting application exit.
    /// </summary>
    public async Task<WindowCloseCheckResult> ResolveCloseDecisionAsync(
        WindowCloseResolutionAction action,
        IAudioRecordingEngine recordingEngine,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recordingEngine);

        var current = EvaluateWindowClose();
        if (current.CanExitImmediately)
        {
            return current;
        }

        if (action == WindowCloseResolutionAction.ReturnToSession)
        {
            return current;
        }

        if (action == WindowCloseResolutionAction.EndSessionAndExit &&
            !string.IsNullOrWhiteSpace(current.ActiveCampaignId) &&
            !string.IsNullOrWhiteSpace(current.ActiveSessionId))
        {
            await recordingEngine.EndSessionRecordingAsync(
                current.ActiveCampaignId,
                current.ActiveSessionId,
                cancellationToken).ConfigureAwait(false);

            NotifySessionState(current.ActiveCampaignId, current.ActiveSessionId, SessionRecordingState.Ended);

            return new WindowCloseCheckResult(
                CanExitImmediately: true,
                RequiresUserConfirmation: false,
                ActiveCampaignId: null,
                ActiveSessionId: null,
                WarningMessageEn: null,
                WarningMessageEs: null,
                AllowedActions: []);
        }

        return current;
    }

    private static WindowCloseCheckResult CreateBlockedResult(string campaignId, string sessionId)
    {
        return new WindowCloseCheckResult(
            CanExitImmediately: false,
            RequiresUserConfirmation: true,
            ActiveCampaignId: campaignId,
            ActiveSessionId: sessionId,
            WarningMessageEn: $"Active session recording '{sessionId}' is in progress for campaign '{campaignId}'. Palantyr cannot exit silently while recording. Choose 'End Session and Exit' to safely close the audio segment, or 'Return to Session' to keep recording.",
            WarningMessageEs: $"La grabación de la sesión '{sessionId}' está activa en la campaña '{campaignId}'. Palantyr no puede cerrarse silenciosamente mientras graba. Elija 'Finalizar Sesión y Salir' para guardar el segmento de audio de forma segura, o 'Volver a la Sesión' para continuar grabando.",
            AllowedActions:
            [
                WindowCloseResolutionAction.EndSessionAndExit,
                WindowCloseResolutionAction.ReturnToSession
            ]);
    }
}