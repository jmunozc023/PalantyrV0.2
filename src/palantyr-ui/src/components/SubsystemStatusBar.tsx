import React from 'react';
import {
  CampaignHomeStatusDto,
  PalantyrLanguage,
  SubsystemProcessingState,
} from '../types/palantyr';
import { TranslationDictionary } from '../i18n/translations';

export interface SubsystemStatusBarProps {
  status: CampaignHomeStatusDto;
  language: PalantyrLanguage;
  t: TranslationDictionary;
}

export const SubsystemStatusBar: React.FC<SubsystemStatusBarProps> = ({
  status,
  language,
  t,
}) => {
  const throttlingReason =
    language === 'Spanish'
      ? status.throttlingReasonEs || status.throttlingReasonEn
      : status.throttlingReasonEn || status.throttlingReasonEs;

  return (
    <section
      className="subsystem-status-section"
      aria-label={t.subsystems.sectionTitle}
    >
      <div className="subsystem-header-bar">
        <div>
          <h3 className="subsystem-section-title">
            {t.subsystems.sectionTitle}
          </h3>
          <p className="subsystem-section-subtitle">
            {t.subsystems.sectionSubtitle}
          </p>
        </div>
        <div className="priority-hierarchy-pill" title="FR-PERF-001">
          🛡 {t.subsystems.priorityTierNote}
        </div>
      </div>

      {/* Storage & Priority Throttling Banners (FR-PERF-001, NFR-REL-008) */}
      {status.isStorageWarningActive && (
        <div className="alert-banner alert-danger" role="alert">
          <span className="alert-icon" aria-hidden="true">
            ⚠
          </span>
          <span>
            {t.subsystems.storageWarningBanner} (
            {status.freeStorageGb.toFixed(1)} GB)
          </span>
        </div>
      )}

      {status.isBackgroundThrottled && (
        <div className="alert-banner alert-warning" role="status">
          <span className="alert-icon" aria-hidden="true">
            ⚡
          </span>
          <span>
            <strong>{t.subsystems.throttledBannerTitle}</strong>{' '}
            {throttlingReason ||
              'Tier 1 audio capture prioritized over background AI.'}
          </span>
        </div>
      )}

      {/* 4 Explicit Independent Subsystem Status Cards (UX-HOME-004, AC-USAB-001) */}
      <div className="subsystem-cards-grid">
        <SubsystemCard
          id="subsystem-recording"
          title={t.subsystems.recordingCardTitle}
          description={t.subsystems.recordingCardDesc}
          state={status.recordingSaveStatus}
          counterLabel={t.subsystems.segmentsSafelyStored}
          counterValue={`${status.safelyStoredSegmentsCount}`}
          t={t}
        />

        <SubsystemCard
          id="subsystem-transcription"
          title={t.subsystems.transcriptionCardTitle}
          description={t.subsystems.transcriptionCardDesc}
          state={status.transcriptionStatus}
          counterLabel={t.subsystems.segmentsTranscribed}
          counterValue={`${status.transcribedSegmentsCount} / ${status.safelyStoredSegmentsCount}`}
          t={t}
        />

        <SubsystemCard
          id="subsystem-memory"
          title={t.subsystems.memoryCardTitle}
          description={t.subsystems.memoryCardDesc}
          state={status.memoryProcessingStatus}
          counterLabel={t.subsystems.segmentsMemoryProcessed}
          counterValue={`${status.memoryProcessedSegmentsCount} / ${status.safelyStoredSegmentsCount}`}
          t={t}
        />

        <SubsystemCard
          id="subsystem-eonix-query"
          title={t.subsystems.eonixCardTitle}
          description={t.subsystems.eonixCardDesc}
          state={status.eonixQueryStatus}
          counterLabel={t.subsystems.freeStorageLabel}
          counterValue={`${status.freeStorageGb.toFixed(1)} GB`}
          t={t}
        />
      </div>
    </section>
  );
};

interface SubsystemCardProps {
  id: string;
  title: string;
  description: string;
  state: SubsystemProcessingState;
  counterLabel: string;
  counterValue: string;
  t: TranslationDictionary;
}

const SubsystemCard: React.FC<SubsystemCardProps> = ({
  id,
  title,
  description,
  state,
  counterLabel,
  counterValue,
  t,
}) => {
  const icon = getSubsystemIcon(state);
  const pattern = t.subsystemPatterns[state];
  const explicitText = t.subsystemStates[state];

  return (
    <article
      id={id}
      className={`subsystem-card subsystem-state-${state.toLowerCase()}`}
      aria-label={`${title}: ${explicitText}`}
    >
      <div className="subsystem-card-top">
        <h4 className="subsystem-card-title">{title}</h4>
        {/* Explicit Text + Icon + Pattern Badge (Never relies on color alone — NFR-USAB-003, AC-USAB-001) */}
        <div
          className={`subsystem-badge badge-state-${state.toLowerCase()}`}
          role="status"
        >
          <span className="badge-icon" aria-hidden="true">
            {icon}
          </span>
          <span className="badge-explicit-label">{explicitText}</span>
          <span className="badge-pattern" aria-hidden="true">
            {pattern}
          </span>
        </div>
      </div>

      <p className="subsystem-card-desc">{description}</p>

      <div className="subsystem-card-footer">
        <span className="subsystem-counter-label">{counterLabel}:</span>
        <strong className="subsystem-counter-value">{counterValue}</strong>
      </div>
    </article>
  );
};

function getSubsystemIcon(state: SubsystemProcessingState): string {
  switch (state) {
    case 'Idle':
      return '○';
    case 'Saved':
      return '🛡';
    case 'Pending':
      return '⏳';
    case 'Processing':
      return '⚙';
    case 'Paused':
      return '⏸';
    case 'Completed':
      return '✓';
    case 'Failed':
      return '✖';
  }
}
