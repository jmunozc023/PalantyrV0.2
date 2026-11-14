import React, { useState } from 'react';
import {
  AppSettingsDto,
  AudioInputDeviceInfo,
  CampaignDeletionOption,
  CampaignSummaryDto,
  CrashRecoveryChoice,
  CrashRecoveryReportDto,
  EndSessionSummaryDto,
  MicrophoneValidationResult,
  PalantyrLanguage,
  PauseTranscriptionBehavior,
  ProcessingMode,
  SystemAssessmentProfile,
} from '../types/palantyr';
import { TranslationDictionary } from '../i18n/translations';

export function formatDurationMs(ms: number): string {
  const totalSec = Math.max(0, Math.floor(ms / 1000));
  const hrs = Math.floor(totalSec / 3600);
  const mins = Math.floor((totalSec % 3600) / 60);
  const secs = totalSec % 60;
  return [hrs, mins, secs].map((v) => String(v).padStart(2, '0')).join(':');
}

/* ============================================================================
 * 1. FirstRunAssessmentModal (FR-SYS-001..005, AC-SYS-001)
 * ============================================================================ */
export interface FirstRunAssessmentModalProps {
  profile: SystemAssessmentProfile;
  language: PalantyrLanguage;
  selectedMode: ProcessingMode;
  onAcceptMode: (mode: ProcessingMode) => void;
  t: TranslationDictionary;
}

export const FirstRunAssessmentModal: React.FC<
  FirstRunAssessmentModalProps
> = ({ profile, language, selectedMode, onAcceptMode, t }) => {
  const [chosenMode, setChosenMode] = useState<ProcessingMode>(
    selectedMode || profile.recommendedMode
  );

  const reasonText =
    language === 'Spanish'
      ? profile.recommendationReasonEs
      : profile.recommendationReasonEn;

  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true">
      <div className="modal-card modal-lg">
        <header className="modal-header">
          <h2>⚙ {t.modals.firstRun.title}</h2>
          <p>{t.modals.firstRun.subtitle}</p>
        </header>

        <div className="modal-body">
          <div className="hardware-specs-grid">
            <div className="spec-box">
              <span className="spec-label">{t.modals.firstRun.cpuLabel}</span>
              <strong>{profile.cpuModel}</strong>
              <small>
                {profile.logicalCores} Logical Cores • {profile.windowsVersion}
              </small>
            </div>

            <div className="spec-box">
              <span className="spec-label">{t.modals.firstRun.ramLabel}</span>
              <strong>
                {profile.availableRamGb.toFixed(1)} GB Free /{' '}
                {profile.totalRamGb.toFixed(1)} GB Total
              </strong>
              <small>Local Whisper &amp; Entity Graph Memory Pool</small>
            </div>

            <div className="spec-box">
              <span className="spec-label">{t.modals.firstRun.gpuLabel}</span>
              <strong>{profile.gpuModel || 'CPU Fallback Only'}</strong>
              <small>
                {profile.dedicatedVramGb.toFixed(1)} GB VRAM •{' '}
                {profile.hasCompatibleGpuAcceleration
                  ? '✓ Hardware Acceleration Ready'
                  : '⚠ No Dedicated AI GPU'}
              </small>
            </div>

            <div className="spec-box">
              <span className="spec-label">
                {t.modals.firstRun.storageLabel}
              </span>
              <strong>{profile.freeStorageGb.toFixed(1)} GB Available</strong>
              <small>.pseg WAL + Isolated SQLite Vaults</small>
            </div>
          </div>

          <div className="spec-devices-box">
            <strong>{t.modals.firstRun.audioDevicesLabel}:</strong>
            <ul>
              {profile.audioDevices.map((d) => (
                <li key={d.deviceId}>
                  🎙 {d.friendlyName} ({d.sampleRateHz} Hz, {d.channels}ch){' '}
                  {d.isDefault ? '★ Default' : ''}
                </li>
              ))}
            </ul>
          </div>

          <div className="recommendation-banner">
            <div className="rec-title">
              ✦ {t.modals.firstRun.recommendedModeBanner}{' '}
              <strong>
                {t.processingModes[profile.recommendedMode].title}
              </strong>
            </div>
            <p>{reasonText}</p>
          </div>

          <div className="form-group">
            <label htmlFor="first-run-mode-select">
              {t.modals.firstRun.selectModeOverrideLabel}
            </label>
            <select
              id="first-run-mode-select"
              value={chosenMode}
              onChange={(e) => setChosenMode(e.target.value as ProcessingMode)}
            >
              {(
                [
                  'LiveBalanced',
                  'RecordingOnly',
                  'ManualDeferred',
                ] as ProcessingMode[]
              ).map((m) => (
                <option key={m} value={m}>
                  {t.processingModes[m].title}
                </option>
              ))}
            </select>
            <small className="field-help">
              {t.processingModes[chosenMode].description}
            </small>
          </div>
        </div>

        <footer className="modal-footer">
          <button
            type="button"
            className="btn btn-gold"
            onClick={() => onAcceptMode(chosenMode)}
          >
            {t.modals.firstRun.acceptAndContinueBtn}
          </button>
        </footer>
      </div>
    </div>
  );
};

/* ============================================================================
 * 2. TwoStageEndSessionModal (UX-END-001..003, AC-END-001..002)
 * ============================================================================ */
export interface TwoStageEndSessionModalProps {
  stage: 1 | 2;
  summary: EndSessionSummaryDto | null;
  onCancelStage1: () => void;
  onConfirmStage1SealAudio: () => Promise<void>;
  onStage2Choice: (continueProcessingNow: boolean) => void;
  t: TranslationDictionary;
}

export const TwoStageEndSessionModal: React.FC<
  TwoStageEndSessionModalProps
> = ({
  stage,
  summary,
  onCancelStage1,
  onConfirmStage1SealAudio,
  onStage2Choice,
  t,
}) => {
  const [isSealing, setIsSealing] = useState(false);

  const handleStage1Confirm = async () => {
    setIsSealing(true);
    try {
      await onConfirmStage1SealAudio();
    } finally {
      setIsSealing(false);
    }
  };

  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true">
      <div className="modal-card">
        {stage === 1 ? (
          <>
            <header className="modal-header">
              <h2>■ {t.modals.endSession.stage1Title}</h2>
              <p>{t.modals.endSession.stage1Warning}</p>
            </header>

            <div className="modal-body">
              <ul className="stage-checklist">
                <li>🛡 {t.modals.endSession.stage1Bullet1}</li>
                <li>🔒 {t.modals.endSession.stage1Bullet2}</li>
                <li>🏠 {t.modals.endSession.stage1Bullet3}</li>
              </ul>
            </div>

            <footer className="modal-footer">
              <button
                type="button"
                className="btn btn-secondary"
                onClick={onCancelStage1}
                disabled={isSealing}
              >
                {t.modals.endSession.stage1CancelBtn}
              </button>
              <button
                type="button"
                className="btn btn-gold"
                onClick={handleStage1Confirm}
                disabled={isSealing}
              >
                {t.modals.endSession.stage1ConfirmBtn}
              </button>
            </footer>
          </>
        ) : (
          <>
            <header className="modal-header">
              <h2>✓ {t.modals.endSession.stage2Title}</h2>
              <p>{t.modals.endSession.stage2Subtitle}</p>
            </header>

            {summary && (
              <div className="modal-body">
                <div className="end-session-metrics-grid">
                  <div className="metric-tile">
                    <span>{t.modals.endSession.totalDurationLabel}</span>
                    <strong>
                      {formatDurationMs(summary.totalRecordedDurationMs)}
                    </strong>
                  </div>
                  <div className="metric-tile tile-safe">
                    <span>{t.modals.endSession.safelyStoredLabel}</span>
                    <strong>✓ {summary.safelyStoredSegments}</strong>
                  </div>
                  <div className="metric-tile">
                    <span>{t.modals.endSession.transcribedLabel}</span>
                    <strong>
                      {summary.transcribedSegments} /{' '}
                      {summary.safelyStoredSegments}
                    </strong>
                  </div>
                  <div className="metric-tile">
                    <span>{t.modals.endSession.memoryProcessedLabel}</span>
                    <strong>
                      {summary.memoryProcessedSegments} /{' '}
                      {summary.safelyStoredSegments}
                    </strong>
                  </div>
                  <div className="metric-tile tile-attention">
                    <span>{t.modals.endSession.needsAttentionLabel}</span>
                    <strong>⚑ {summary.needsAttentionItemsCount}</strong>
                  </div>
                </div>

                <p className="stage2-note">
                  {t.modals.endSession.finishLaterNote}
                </p>
              </div>
            )}

            <footer className="modal-footer">
              <button
                type="button"
                className="btn btn-secondary"
                onClick={() => onStage2Choice(false)}
              >
                {t.modals.endSession.finishLaterBtn}
              </button>
              <button
                type="button"
                className="btn btn-gold"
                onClick={() => onStage2Choice(true)}
              >
                {t.modals.endSession.continueProcessingBtn}
              </button>
            </footer>
          </>
        )}
      </div>
    </div>
  );
};

/* ============================================================================
 * 3. CrashRecoveryModal (FR-REC-006..008, AC-REC-003)
 * ============================================================================ */
export interface CrashRecoveryModalProps {
  report: CrashRecoveryReportDto;
  language: PalantyrLanguage;
  onResolveChoice: (choice: CrashRecoveryChoice) => void;
  t: TranslationDictionary;
}

export const CrashRecoveryModal: React.FC<CrashRecoveryModalProps> = ({
  report,
  language,
  onResolveChoice,
  t,
}) => {
  const summaryMessage =
    language === 'Spanish' ? report.summaryMessageEs : report.summaryMessageEn;

  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true">
      <div className="modal-card modal-lg">
        <header className="modal-header">
          <h2>⚡ {t.modals.crashRecovery.title}</h2>
          <p>{t.modals.crashRecovery.subtitle}</p>
        </header>

        <div className="modal-body">
          <div className="alert-banner alert-warning">{summaryMessage}</div>

          <div className="hardware-specs-grid">
            <div className="spec-box">
              <span className="spec-label">
                {t.modals.crashRecovery.previouslySafeLabel}
              </span>
              <strong>✓ {report.previouslySafeSegmentsCount}</strong>
            </div>
            <div className="spec-box">
              <span className="spec-label">
                {t.modals.crashRecovery.recoveredFromWalLabel}
              </span>
              <strong>🛡 {report.recoveredSegmentsCount}</strong>
            </div>
            <div className="spec-box">
              <span className="spec-label">
                {t.modals.crashRecovery.unrecoverableLabel}
              </span>
              <strong>{report.unrecoverableSegmentsCount}</strong>
            </div>
            <div className="spec-box">
              <span className="spec-label">
                {t.modals.crashRecovery.recoveredDurationLabel}
              </span>
              <strong>
                {formatDurationMs(report.totalRecoveredDurationMs)}
              </strong>
            </div>
          </div>

          <h4 className="wal-table-heading">
            {t.modals.crashRecovery.walTableTitle}
          </h4>
          <table className="wal-ledger-table">
            <thead>
              <tr>
                <th>{t.modals.crashRecovery.colSeq}</th>
                <th>{t.modals.crashRecovery.colSegment}</th>
                <th>{t.modals.crashRecovery.colDuration}</th>
                <th>{t.modals.crashRecovery.colStatus}</th>
              </tr>
            </thead>
            <tbody>
              {report.segments.map((seg) => (
                <tr key={seg.segmentId}>
                  <td>#{seg.sequenceNumber}</td>
                  <td>
                    <code>{seg.filePath}</code>
                  </td>
                  <td>{formatDurationMs(seg.durationMs)}</td>
                  <td>
                    <span className={`wal-status-pill wal-${seg.status}`}>
                      {seg.status}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          <div className="recovery-choices-stack">
            <button
              type="button"
              className="recovery-option-btn option-primary"
              onClick={() => onResolveChoice('ContinueSession')}
            >
              <strong>▶ {t.modals.crashRecovery.choiceContinueTitle}</strong>
              <span>{t.modals.crashRecovery.choiceContinueDesc}</span>
            </button>

            <button
              type="button"
              className="recovery-option-btn"
              onClick={() => onResolveChoice('EndSessionAndProcess')}
            >
              <strong>
                ⚙ {t.modals.crashRecovery.choiceEndAndProcessTitle}
              </strong>
              <span>{t.modals.crashRecovery.choiceEndAndProcessDesc}</span>
            </button>

            <button
              type="button"
              className="recovery-option-btn"
              onClick={() => onResolveChoice('CloseSafelyAndStartNew')}
            >
              <strong>
                📁 {t.modals.crashRecovery.choiceCloseAndStartNewTitle}
              </strong>
              <span>{t.modals.crashRecovery.choiceCloseAndStartNewDesc}</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

/* ============================================================================
 * 4. SafeCampaignDeleteModal (Section 38, AC-DATA-006)
 * ============================================================================ */
export interface SafeCampaignDeleteModalProps {
  campaign: CampaignSummaryDto;
  onSelectDeletionOption: (option: CampaignDeletionOption) => void;
  t: TranslationDictionary;
}

export const SafeCampaignDeleteModal: React.FC<
  SafeCampaignDeleteModalProps
> = ({ campaign, onSelectDeletionOption, t }) => {
  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true">
      <div className="modal-card">
        <header className="modal-header">
          <h2>🛡 {t.modals.safeDelete.title}</h2>
          <p>{t.modals.safeDelete.subtitle}</p>
        </header>

        <div className="modal-body">
          <div className="delete-target-callout">
            <span>{t.modals.safeDelete.confirmNameLabel}</span>
            <strong>{campaign.name}</strong>
            <code>{campaign.databaseName}</code>
          </div>
          <p>{t.modals.safeDelete.warningText}</p>

          <div className="recovery-choices-stack">
            <button
              type="button"
              className="btn btn-gold"
              onClick={() => onSelectDeletionOption('CreateBackupAndDelete')}
            >
              {t.modals.safeDelete.backupAndDeleteBtn}
            </button>

            <button
              type="button"
              className="btn btn-danger"
              onClick={() => onSelectDeletionOption('DeleteWithoutBackup')}
            >
              {t.modals.safeDelete.deleteWithoutBackupBtn}
            </button>

            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => onSelectDeletionOption('Cancel')}
            >
              {t.modals.safeDelete.cancelBtn}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

/* ============================================================================
 * 5. WindowCloseGuardModal (NFR-REL-009, AC-REL-004)
 * ============================================================================ */
export interface WindowCloseGuardModalProps {
  onReturnToSession: () => void;
  onEndSessionAndExit: () => void;
  t: TranslationDictionary;
}

export const WindowCloseGuardModal: React.FC<WindowCloseGuardModalProps> = ({
  onReturnToSession,
  onEndSessionAndExit,
  t,
}) => {
  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true">
      <div className="modal-card">
        <header className="modal-header">
          <h2>🛑 {t.modals.windowCloseGuard.title}</h2>
          <p>{t.modals.windowCloseGuard.subtitle}</p>
        </header>

        <div className="modal-body">
          <div className="alert-banner alert-danger">
            {t.modals.windowCloseGuard.warningBody}
          </div>
        </div>

        <footer className="modal-footer">
          <button
            type="button"
            className="btn btn-gold"
            onClick={onReturnToSession}
          >
            {t.modals.windowCloseGuard.returnToSessionBtn}
          </button>
          <button
            type="button"
            className="btn btn-danger"
            onClick={onEndSessionAndExit}
          >
            {t.modals.windowCloseGuard.endSessionAndExitBtn}
          </button>
        </footer>
      </div>
    </div>
  );
};

/* ============================================================================
 * 6. SettingsModal (Section 38, AC-DATA-004, AC-USAB-004, NFR-DATA-006)
 * ============================================================================ */
export interface SettingsModalProps {
  settings: AppSettingsDto;
  audioDevices: AudioInputDeviceInfo[];
  onSaveSettings: (updated: AppSettingsDto) => Promise<void>;
  onTestMicrophone: (deviceId: string) => Promise<MicrophoneValidationResult>;
  onRerunAssessment: () => void;
  onClose: () => void;
  t: TranslationDictionary;
}

export const SettingsModal: React.FC<SettingsModalProps> = ({
  settings,
  audioDevices,
  onSaveSettings,
  onTestMicrophone,
  onRerunAssessment,
  onClose,
  t,
}) => {
  const [draft, setDraft] = useState<AppSettingsDto>({ ...settings });
  const [micResult, setMicResult] =
    useState<MicrophoneValidationResult | null>(null);
  const [testingMic, setTestingMic] = useState(false);

  const handleToggleKeepAudio = (checked: boolean) => {
    setDraft((prev) => ({
      ...prev,
      keepOriginalSessionAudio: checked,
      keepAudioWarningAcknowledged: checked
        ? prev.keepAudioWarningAcknowledged
        : false,
    }));
  };

  const handleTestMic = async () => {
    const devId =
      draft.selectedAudioDeviceId ||
      audioDevices[0]?.deviceId ||
      'mic-default-wasapi';
    setTestingMic(true);
    try {
      const res = await onTestMicrophone(devId);
      setMicResult(res);
    } finally {
      setTestingMic(false);
    }
  };

  const canSave =
    !draft.keepOriginalSessionAudio || draft.keepAudioWarningAcknowledged;

  const handleSave = async () => {
    if (!canSave) return;
    await onSaveSettings(draft);
    onClose();
  };

  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true">
      <div className="modal-card modal-lg">
        <header className="modal-header">
          <h2>⚙ {t.modals.settings.title}</h2>
          <p>{t.modals.settings.subtitle}</p>
        </header>

        <div className="modal-body settings-body">
          <div className="wizard-form-grid">
            <div className="form-group">
              <label htmlFor="set-ui-lang">
                {t.modals.settings.uiLanguageLabel}
              </label>
              <select
                id="set-ui-lang"
                value={draft.uiLanguage}
                onChange={(e) =>
                  setDraft({
                    ...draft,
                    uiLanguage: e.target.value as PalantyrLanguage,
                  })
                }
              >
                <option value="English">English (EN)</option>
                <option value="Spanish">Español (ES)</option>
              </select>
            </div>

            <div className="form-group">
              <label htmlFor="set-proc-mode">
                {t.modals.settings.processingModeLabel}
              </label>
              <select
                id="set-proc-mode"
                value={draft.activeProcessingMode}
                onChange={(e) =>
                  setDraft({
                    ...draft,
                    activeProcessingMode: e.target.value as ProcessingMode,
                  })
                }
              >
                {(
                  [
                    'LiveBalanced',
                    'RecordingOnly',
                    'ManualDeferred',
                  ] as ProcessingMode[]
                ).map((m) => (
                  <option key={m} value={m}>
                    {t.processingModes[m].title}
                  </option>
                ))}
              </select>
            </div>

            <div className="form-group">
              <label htmlFor="set-pause-pref">
                {t.modals.settings.pauseBehaviorLabel}
              </label>
              <select
                id="set-pause-pref"
                value={draft.pausePreference}
                onChange={(e) =>
                  setDraft({
                    ...draft,
                    pausePreference: e.target
                      .value as PauseTranscriptionBehavior,
                  })
                }
              >
                {(
                  ['Never', 'Ask', 'Always'] as PauseTranscriptionBehavior[]
                ).map((p) => (
                  <option key={p} value={p}>
                    {t.pauseBehaviors[p].title}
                  </option>
                ))}
              </select>
            </div>

            <div className="form-group">
              <label htmlFor="set-mic-device">
                {t.modals.settings.microphoneDeviceLabel}
              </label>
              <div className="inline-control-row">
                <select
                  id="set-mic-device"
                  value={
                    draft.selectedAudioDeviceId ||
                    audioDevices[0]?.deviceId ||
                    ''
                  }
                  onChange={(e) =>
                    setDraft({
                      ...draft,
                      selectedAudioDeviceId: e.target.value,
                    })
                  }
                >
                  {audioDevices.map((d) => (
                    <option key={d.deviceId} value={d.deviceId}>
                      {d.friendlyName}
                    </option>
                  ))}
                </select>
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={handleTestMic}
                  disabled={testingMic}
                >
                  {t.modals.settings.testMicBtn}
                </button>
              </div>
              {micResult && (
                <small className="field-help">
                  {draft.uiLanguage === 'Spanish'
                    ? micResult.diagnosticMessageEs
                    : micResult.diagnosticMessageEn}{' '}
                  ({micResult.peakLevelDb.toFixed(1)} dB)
                </small>
              )}
            </div>
          </div>

          <hr className="settings-divider" />

          {/* Accessibility & Reduced Motion (NFR-USAB-009, AC-USAB-004) */}
          <div className="settings-section">
            <h4>{t.modals.settings.accessibilitySection}</h4>
            <label className="checkbox-row">
              <input
                type="checkbox"
                checked={draft.reduceAnimations}
                onChange={(e) =>
                  setDraft({ ...draft, reduceAnimations: e.target.checked })
                }
              />
              <div>
                <strong>{t.modals.settings.reduceAnimationsLabel}</strong>
                <p className="field-help">
                  {t.modals.settings.reduceAnimationsDesc}
                </p>
              </div>
            </label>
          </div>

          <hr className="settings-divider" />

          {/* Keep Original Session Audio + Mandatory Disk Space Warning (NFR-DATA-006, AC-DATA-004) */}
          <div className="settings-section">
            <h4>{t.modals.settings.storageAndRetentionSection}</h4>
            <label className="checkbox-row">
              <input
                type="checkbox"
                checked={draft.keepOriginalSessionAudio}
                onChange={(e) => handleToggleKeepAudio(e.target.checked)}
              />
              <div>
                <strong>{t.modals.settings.keepOriginalAudioLabel}</strong>
              </div>
            </label>

            {draft.keepOriginalSessionAudio && (
              <div
                className="disk-space-warning-box"
                role="alert"
                aria-live="assertive"
              >
                <strong>
                  ⚠ {t.modals.settings.keepOriginalAudioWarningTitle}
                </strong>
                <p>{t.modals.settings.keepOriginalAudioWarningBody}</p>
                <label className="checkbox-row ack-checkbox">
                  <input
                    type="checkbox"
                    checked={draft.keepAudioWarningAcknowledged}
                    onChange={(e) =>
                      setDraft({
                        ...draft,
                        keepAudioWarningAcknowledged: e.target.checked,
                      })
                    }
                  />
                  <span>
                    {t.modals.settings.acknowledgeAudioWarningCheckbox}
                  </span>
                </label>
              </div>
            )}
          </div>
        </div>

        <footer className="modal-footer settings-footer">
          <button
            type="button"
            className="btn btn-secondary"
            onClick={onRerunAssessment}
          >
            {t.modals.settings.rerunAssessmentBtn}
          </button>

          <div className="wizard-footer-right">
            <button type="button" className="btn btn-ghost" onClick={onClose}>
              {t.modals.settings.closeBtn}
            </button>
            <button
              type="button"
              className="btn btn-gold"
              disabled={!canSave}
              onClick={handleSave}
            >
              {t.modals.settings.saveSettingsBtn}
            </button>
          </div>
        </footer>
      </div>
    </div>
  );
};
