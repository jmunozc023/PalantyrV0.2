import React, { useState } from 'react';
import {
  AudioInputDeviceInfo,
  CreateCampaignRequest,
  MicrophoneValidationResult,
  PalantyrLanguage,
  PartyMemberDto,
  PartyMembershipStatus,
  VisibilityScope,
  WorldLoreEntryDto,
} from '../types/palantyr';
import { TranslationDictionary } from '../i18n/translations';

export interface CreateCampaignWizardProps {
  defaultLanguage: PalantyrLanguage;
  audioDevices: AudioInputDeviceInfo[];
  onTestMicrophone: (deviceId: string) => Promise<MicrophoneValidationResult>;
  onCreateCampaign: (request: CreateCampaignRequest) => Promise<void>;
  onCancel: () => void;
  t: TranslationDictionary;
}

export const CreateCampaignWizard: React.FC<CreateCampaignWizardProps> = ({
  defaultLanguage,
  audioDevices,
  onTestMicrophone,
  onCreateCampaign,
  onCancel,
  t,
}) => {
  const [step, setStep] = useState<number>(1);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Step 1: Basics
  const [name, setName] = useState('');
  const [subtitle, setSubtitle] = useState('');
  const [primaryLanguage, setPrimaryLanguage] =
    useState<PalantyrLanguage>(defaultLanguage);

  // Step 2: World & Calendar
  const [worldSummary, setWorldSummary] = useState('');
  const [calendarDescription, setCalendarDescription] = useState('');

  // Step 3: Party Roster (FR-CAM-001..005)
  const [partyRoster, setPartyRoster] = useState<PartyMemberDto[]>([
    {
      characterId: 'char-seed-1',
      characterName: 'Aeliana Starweaver',
      raceOrSpecies: 'High Elf',
      classOrArchetype: 'Chronurgy Wizard',
      controlledByPlayerName: 'Nara',
      isPlayerCharacter: true,
      currentStatus: 'Active',
      membershipHistory: [
        {
          status: 'Active',
          changedAtUtc: new Date().toISOString(),
          sessionId: null,
          campaignDateContext: 'Campaign Founding',
          reasonNote: 'Founding party member',
        },
      ],
    },
  ]);

  const [newCharName, setNewCharName] = useState('');
  const [newCharRace, setNewCharRace] = useState('');
  const [newCharClass, setNewCharClass] = useState('');
  const [newCharPlayer, setNewCharPlayer] = useState('');
  const [newCharIsPc, setNewCharIsPc] = useState(true);
  const [newCharStatus, setNewCharStatus] =
    useState<PartyMembershipStatus>('Active');

  // Step 4: Initial World Lore (FR-SETUP-001..006)
  const [worldLore, setWorldLore] = useState<WorldLoreEntryDto[]>([
    {
      loreId: 'lore-seed-1',
      title: 'The Shattered Crown of Valen',
      category: 'World History',
      content:
        'Three centuries ago, the Obsidian Crown was sundered into five shards. The public believes all shards were lost at sea.',
      visibility: 'GeneralWorld',
      knowledgeState: 'CampaignTruth',
      confidence: 'DMConfirmed',
      sourceType: 'CampaignSetup',
      createdAtUtc: new Date().toISOString(),
    },
    {
      loreId: 'lore-seed-2',
      title: 'True Identity of Arch-Prelate Vane',
      category: 'Villain Secret',
      content:
        'Arch-Prelate Vane secretly holds the Crimson Shard beneath the Cathedral vault and orchestrates the Cult of Ash.',
      visibility: 'DMPrivate',
      knowledgeState: 'DMPrivateTruth',
      confidence: 'DMConfirmed',
      sourceType: 'CampaignSetup',
      createdAtUtc: new Date().toISOString(),
    },
  ]);

  const [newLoreTitle, setNewLoreTitle] = useState('');
  const [newLoreCategory, setNewLoreCategory] = useState('');
  const [newLoreVisibility, setNewLoreVisibility] =
    useState<VisibilityScope>('GeneralWorld');
  const [newLoreContent, setNewLoreContent] = useState('');

  // Step 5: Microphone Selection & Signal Validation (FR-REC-001)
  const defaultMic =
    audioDevices.find((d) => d.isDefault)?.deviceId ||
    audioDevices[0]?.deviceId ||
    'mic-default-wasapi';
  const [selectedMicId, setSelectedMicId] = useState<string>(defaultMic);
  const [isTestingMic, setIsTestingMic] = useState(false);
  const [micResult, setMicResult] =
    useState<MicrophoneValidationResult | null>(null);

  const stepLabels = [
    t.wizard.steps.basics,
    t.wizard.steps.world,
    t.wizard.steps.roster,
    t.wizard.steps.lore,
    t.wizard.steps.microphone,
    t.wizard.steps.review,
  ];

  const handleAddMember = () => {
    const trimmedName = newCharName.trim();
    if (!trimmedName) return;
    const nowIso = new Date().toISOString();
    const member: PartyMemberDto = {
      characterId: `char-${Date.now()}`,
      characterName: trimmedName,
      raceOrSpecies: newCharRace.trim() || 'Unknown',
      classOrArchetype: newCharClass.trim() || 'Adventurer',
      controlledByPlayerName: newCharIsPc
        ? newCharPlayer.trim() || 'Player'
        : null,
      isPlayerCharacter: newCharIsPc,
      currentStatus: newCharStatus,
      membershipHistory: [
        {
          status: newCharStatus,
          changedAtUtc: nowIso,
          sessionId: null,
          campaignDateContext: 'Campaign Setup',
          reasonNote: 'Added in Guided Create Campaign Wizard',
        },
      ],
    };
    setPartyRoster((prev) => [...prev, member]);
    setNewCharName('');
    setNewCharRace('');
    setNewCharClass('');
    setNewCharPlayer('');
  };

  const handleRemoveMember = (id: string) => {
    setPartyRoster((prev) => prev.filter((m) => m.characterId !== id));
  };

  const handleAddLore = () => {
    const trimmedTitle = newLoreTitle.trim();
    const trimmedContent = newLoreContent.trim();
    if (!trimmedTitle || !trimmedContent) return;

    const entry: WorldLoreEntryDto = {
      loreId: `lore-${Date.now()}`,
      title: trimmedTitle,
      category: newLoreCategory.trim() || 'General',
      content: trimmedContent,
      visibility: newLoreVisibility,
      knowledgeState:
        newLoreVisibility === 'DMPrivate' ? 'DMPrivateTruth' : 'CampaignTruth',
      confidence: 'DMConfirmed',
      sourceType: 'CampaignSetup',
      createdAtUtc: new Date().toISOString(),
    };
    setWorldLore((prev) => [...prev, entry]);
    setNewLoreTitle('');
    setNewLoreCategory('');
    setNewLoreContent('');
  };

  const handleRemoveLore = (id: string) => {
    setWorldLore((prev) => prev.filter((l) => l.loreId !== id));
  };

  const handleRunMicTest = async () => {
    setIsTestingMic(true);
    try {
      const res = await onTestMicrophone(selectedMicId);
      setMicResult(res);
    } finally {
      setIsTestingMic(false);
    }
  };

  const handleFinishWizard = async () => {
    if (!name.trim()) {
      setStep(1);
      return;
    }
    setIsSubmitting(true);
    try {
      const req: CreateCampaignRequest = {
        name: name.trim(),
        subtitleOrSettingName: subtitle.trim() || null,
        worldSummary: worldSummary.trim() || null,
        calendarSystemDescription: calendarDescription.trim() || null,
        primaryLanguage,
        initialPartyRoster: partyRoster,
        initialWorldLore: worldLore,
        preferredMicrophoneDeviceId: selectedMicId,
      };
      await onCreateCampaign(req);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="wizard-container" aria-label={t.wizard.title}>
      <header className="wizard-header">
        <div>
          <h2 className="wizard-title">{t.wizard.title}</h2>
          <p className="wizard-subtitle">{t.wizard.subtitle}</p>
        </div>
        <span className="wizard-step-counter">
          {t.wizard.stepOf} {step} / 6
        </span>
      </header>

      {/* 6-Step Progress Stepper */}
      <nav className="wizard-stepper" aria-label="Campaign Creation Steps">
        {stepLabels.map((label, idx) => {
          const stepNum = idx + 1;
          const isCurrent = step === stepNum;
          const isCompleted = step > stepNum;
          return (
            <button
              key={label}
              type="button"
              className={`wizard-step-tab ${isCurrent ? 'current' : ''} ${
                isCompleted ? 'completed' : ''
              }`}
              onClick={() => {
                if (stepNum === 1 || name.trim().length > 0) {
                  setStep(stepNum);
                }
              }}
            >
              <span className="step-check">
                {isCompleted ? '✓' : stepNum}
              </span>{' '}
              <span>{label}</span>
            </button>
          );
        })}
      </nav>

      {/* Step Content Body */}
      <div className="wizard-body">
        {step === 1 && (
          <div className="wizard-step-pane">
            <div className="form-group">
              <label htmlFor="wiz-camp-name">
                {t.wizard.basics.campaignNameLabel}
              </label>
              <input
                id="wiz-camp-name"
                type="text"
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder={t.wizard.basics.campaignNamePlaceholder}
                required
              />
            </div>

            <div className="form-group">
              <label htmlFor="wiz-camp-subtitle">
                {t.wizard.basics.subtitleLabel}
              </label>
              <input
                id="wiz-camp-subtitle"
                type="text"
                value={subtitle}
                onChange={(e) => setSubtitle(e.target.value)}
                placeholder={t.wizard.basics.subtitlePlaceholder}
              />
            </div>

            <div className="form-group">
              <label htmlFor="wiz-camp-lang">
                {t.wizard.basics.primaryLanguageLabel}
              </label>
              <select
                id="wiz-camp-lang"
                value={primaryLanguage}
                onChange={(e) =>
                  setPrimaryLanguage(e.target.value as PalantyrLanguage)
                }
              >
                <option value="English">English (EN)</option>
                <option value="Spanish">Español (ES)</option>
              </select>
              <small className="field-help">
                {t.wizard.basics.primaryLanguageHelp}
              </small>
            </div>
          </div>
        )}

        {step === 2 && (
          <div className="wizard-step-pane">
            <div className="form-group">
              <label htmlFor="wiz-world-summary">
                {t.wizard.world.worldSummaryLabel}
              </label>
              <textarea
                id="wiz-world-summary"
                rows={4}
                value={worldSummary}
                onChange={(e) => setWorldSummary(e.target.value)}
                placeholder={t.wizard.world.worldSummaryPlaceholder}
              />
            </div>

            <div className="form-group">
              <label htmlFor="wiz-calendar">
                {t.wizard.world.calendarSystemLabel}
              </label>
              <input
                id="wiz-calendar"
                type="text"
                value={calendarDescription}
                onChange={(e) => setCalendarDescription(e.target.value)}
                placeholder={t.wizard.world.calendarSystemPlaceholder}
              />
              <small className="field-help">
                {t.wizard.world.calendarHelp}
              </small>
            </div>
          </div>
        )}

        {step === 3 && (
          <div className="wizard-step-pane">
            <h3>{t.wizard.roster.heading}</h3>
            <p className="pane-subheading">{t.wizard.roster.subheading}</p>

            <div className="wizard-form-grid">
              <div className="form-group">
                <label htmlFor="wiz-char-name">
                  {t.wizard.roster.charNameLabel}
                </label>
                <input
                  id="wiz-char-name"
                  type="text"
                  value={newCharName}
                  onChange={(e) => setNewCharName(e.target.value)}
                  placeholder={t.wizard.roster.charNamePlaceholder}
                />
              </div>

              <div className="form-group">
                <label htmlFor="wiz-char-race">
                  {t.wizard.roster.raceLabel}
                </label>
                <input
                  id="wiz-char-race"
                  type="text"
                  value={newCharRace}
                  onChange={(e) => setNewCharRace(e.target.value)}
                  placeholder={t.wizard.roster.racePlaceholder}
                />
              </div>

              <div className="form-group">
                <label htmlFor="wiz-char-class">
                  {t.wizard.roster.classLabel}
                </label>
                <input
                  id="wiz-char-class"
                  type="text"
                  value={newCharClass}
                  onChange={(e) => setNewCharClass(e.target.value)}
                  placeholder={t.wizard.roster.classPlaceholder}
                />
              </div>

              <div className="form-group">
                <label htmlFor="wiz-char-type">
                  {t.wizard.roster.memberTypeLabel}
                </label>
                <select
                  id="wiz-char-type"
                  value={newCharIsPc ? 'pc' : 'npc'}
                  onChange={(e) => {
                    const isPc = e.target.value === 'pc';
                    setNewCharIsPc(isPc);
                    if (!isPc) setNewCharStatus('Temporary');
                    else setNewCharStatus('Active');
                  }}
                >
                  <option value="pc">{t.wizard.roster.pcOption}</option>
                  <option value="npc">{t.wizard.roster.tempNpcOption}</option>
                </select>
              </div>

              {newCharIsPc && (
                <div className="form-group">
                  <label htmlFor="wiz-char-player">
                    {t.wizard.roster.playerLabel}
                  </label>
                  <input
                    id="wiz-char-player"
                    type="text"
                    value={newCharPlayer}
                    onChange={(e) => setNewCharPlayer(e.target.value)}
                    placeholder={t.wizard.roster.playerPlaceholder}
                  />
                </div>
              )}

              <div className="form-group">
                <label htmlFor="wiz-char-status">
                  {t.wizard.roster.statusLabel}
                </label>
                <select
                  id="wiz-char-status"
                  value={newCharStatus}
                  onChange={(e) =>
                    setNewCharStatus(e.target.value as PartyMembershipStatus)
                  }
                >
                  {(
                    [
                      'Active',
                      'Temporary',
                      'Former',
                      'Deceased',
                      'Inactive',
                    ] as PartyMembershipStatus[]
                  ).map((st) => (
                    <option key={st} value={st}>
                      {t.partyStatuses[st]}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <button
              type="button"
              className="btn btn-secondary"
              onClick={handleAddMember}
            >
              {t.wizard.roster.addMemberBtn}
            </button>

            <div className="wizard-items-list">
              {partyRoster.length === 0 ? (
                <p className="empty-note">{t.wizard.roster.emptyRosterNote}</p>
              ) : (
                partyRoster.map((m) => (
                  <div key={m.characterId} className="wizard-item-row">
                    <div>
                      <strong>{m.characterName}</strong> — {m.raceOrSpecies}{' '}
                      {m.classOrArchetype}
                      <span className="item-tag">
                        {m.isPlayerCharacter
                          ? `PC (${m.controlledByPlayerName || 'Player'})`
                          : t.campaignHome.dmTemporaryNpc}
                      </span>
                      <span className={`status-pill status-${m.currentStatus}`}>
                        {t.partyStatuses[m.currentStatus]}
                      </span>
                    </div>
                    <button
                      type="button"
                      className="btn-link-danger"
                      onClick={() => handleRemoveMember(m.characterId)}
                    >
                      {t.wizard.roster.removeMemberBtn}
                    </button>
                  </div>
                ))
              )}
            </div>
          </div>
        )}

        {step === 4 && (
          <div className="wizard-step-pane">
            <h3>{t.wizard.lore.heading}</h3>
            <p className="pane-subheading">{t.wizard.lore.subheading}</p>

            <div className="wizard-form-grid">
              <div className="form-group">
                <label htmlFor="wiz-lore-title">
                  {t.wizard.lore.titleLabel}
                </label>
                <input
                  id="wiz-lore-title"
                  type="text"
                  value={newLoreTitle}
                  onChange={(e) => setNewLoreTitle(e.target.value)}
                  placeholder={t.wizard.lore.titlePlaceholder}
                />
              </div>

              <div className="form-group">
                <label htmlFor="wiz-lore-category">
                  {t.wizard.lore.categoryLabel}
                </label>
                <input
                  id="wiz-lore-category"
                  type="text"
                  value={newLoreCategory}
                  onChange={(e) => setNewLoreCategory(e.target.value)}
                  placeholder={t.wizard.lore.categoryPlaceholder}
                />
              </div>

              <div className="form-group">
                <label htmlFor="wiz-lore-vis">
                  {t.wizard.lore.visibilityLabel}
                </label>
                <select
                  id="wiz-lore-vis"
                  value={newLoreVisibility}
                  onChange={(e) =>
                    setNewLoreVisibility(e.target.value as VisibilityScope)
                  }
                >
                  {(
                    [
                      'GeneralWorld',
                      'PartyKnown',
                      'DMPrivate',
                    ] as VisibilityScope[]
                  ).map((vis) => (
                    <option key={vis} value={vis}>
                      {t.visibilityScopes[vis].label}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div className="form-group">
              <label htmlFor="wiz-lore-content">
                {t.wizard.lore.contentLabel}
              </label>
              <textarea
                id="wiz-lore-content"
                rows={3}
                value={newLoreContent}
                onChange={(e) => setNewLoreContent(e.target.value)}
                placeholder={t.wizard.lore.contentPlaceholder}
              />
            </div>

            <button
              type="button"
              className="btn btn-secondary"
              onClick={handleAddLore}
            >
              {t.wizard.lore.addLoreBtn}
            </button>

            <div className="wizard-items-list">
              {worldLore.length === 0 ? (
                <p className="empty-note">{t.wizard.lore.emptyLoreNote}</p>
              ) : (
                worldLore.map((l) => (
                  <div key={l.loreId} className="wizard-item-row">
                    <div>
                      <strong>{l.title}</strong> ({l.category})
                      <span className={`scope-tag scope-${l.visibility}`}>
                        {t.visibilityScopes[l.visibility].label}
                      </span>
                      <p className="lore-snippet">{l.content}</p>
                    </div>
                    <button
                      type="button"
                      className="btn-link-danger"
                      onClick={() => handleRemoveLore(l.loreId)}
                    >
                      {t.wizard.lore.removeLoreBtn}
                    </button>
                  </div>
                ))
              )}
            </div>
          </div>
        )}

        {step === 5 && (
          <div className="wizard-step-pane">
            <h3>{t.wizard.microphone.heading}</h3>
            <p className="pane-subheading">{t.wizard.microphone.subheading}</p>

            <div className="form-group">
              <label htmlFor="wiz-mic-device">
                {t.wizard.microphone.selectDeviceLabel}
              </label>
              <select
                id="wiz-mic-device"
                value={selectedMicId}
                onChange={(e) => setSelectedMicId(e.target.value)}
              >
                {audioDevices.map((d) => (
                  <option key={d.deviceId} value={d.deviceId}>
                    {d.friendlyName} ({d.sampleRateHz} Hz, {d.channels}ch)
                    {d.isDefault ? ' ★ Default' : ''}
                  </option>
                ))}
              </select>
            </div>

            <div className="mic-test-action-row">
              <button
                type="button"
                className="btn btn-gold"
                onClick={handleRunMicTest}
                disabled={isTestingMic}
              >
                {isTestingMic
                  ? t.wizard.microphone.testingSignal
                  : t.wizard.microphone.runSignalTestBtn}
              </button>
            </div>

            {micResult && (
              <div
                className={`mic-validation-card ${
                  micResult.hasDetectableSignal ? 'signal-ok' : 'signal-warn'
                }`}
                role="status"
              >
                <div className="mic-badge-row">
                  <strong>
                    {micResult.hasDetectableSignal
                      ? t.wizard.microphone.signalDetectedBadge
                      : t.wizard.microphone.noSignalBadge}
                  </strong>
                  <span>
                    {t.wizard.microphone.peakLevelLabel}:{' '}
                    <code>{micResult.peakLevelDb.toFixed(1)} dB</code>
                  </span>
                </div>
                <p>
                  {primaryLanguage === 'Spanish'
                    ? micResult.diagnosticMessageEs
                    : micResult.diagnosticMessageEn}
                </p>
              </div>
            )}

            <div className="wal-protection-callout">
              🛡 {t.wizard.microphone.walProtectionNote}
            </div>
          </div>
        )}

        {step === 6 && (
          <div className="wizard-step-pane">
            <h3>{t.wizard.review.heading}</h3>
            <p className="pane-subheading">{t.wizard.review.subheading}</p>

            <div className="review-summary-grid">
              <div className="review-card">
                <h4>{t.wizard.review.summaryBasics}</h4>
                <p>
                  <strong>{name || '(Unnamed Campaign)'}</strong>
                  {subtitle ? ` — ${subtitle}` : ''}
                </p>
                <p>
                  Language: <code>{primaryLanguage}</code>
                </p>
                <p>
                  {t.wizard.review.dbIsolationNotice}{' '}
                  <code>
                    campaign_
                    {(name || 'new')
                      .toLowerCase()
                      .replace(/[^a-z0-9]+/g, '_')
                      .slice(0, 16)}
                    .db
                  </code>
                </p>
              </div>

              <div className="review-card">
                <h4>
                  {t.wizard.review.summaryParty} ({partyRoster.length})
                </h4>
                <ul>
                  {partyRoster.map((m) => (
                    <li key={m.characterId}>
                      {m.characterName} ({m.classOrArchetype}) —{' '}
                      {t.partyStatuses[m.currentStatus]}
                    </li>
                  ))}
                </ul>
              </div>

              <div className="review-card">
                <h4>
                  {t.wizard.review.summaryLore} ({worldLore.length})
                </h4>
                <ul>
                  {worldLore.map((l) => (
                    <li key={l.loreId}>
                      {l.title} [{t.visibilityScopes[l.visibility].label}]
                    </li>
                  ))}
                </ul>
              </div>

              <div className="review-card">
                <h4>{t.wizard.review.summaryAudio}</h4>
                <p>
                  Device:{' '}
                  <code>
                    {audioDevices.find((d) => d.deviceId === selectedMicId)
                      ?.friendlyName || selectedMicId}
                  </code>
                </p>
              </div>
            </div>
          </div>
        )}
      </div>

      {/* Wizard Footer Controls */}
      <footer className="wizard-footer">
        <button type="button" className="btn btn-ghost" onClick={onCancel}>
          {t.wizard.cancelBtn}
        </button>

        <div className="wizard-footer-right">
          {step > 1 && (
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => setStep((s) => s - 1)}
            >
              {t.wizard.backBtn}
            </button>
          )}

          {step < 6 ? (
            <button
              type="button"
              className="btn btn-gold"
              disabled={step === 1 && !name.trim()}
              onClick={() => setStep((s) => s + 1)}
            >
              {t.wizard.nextBtn}
            </button>
          ) : (
            <button
              type="button"
              className="btn btn-gold"
              disabled={!name.trim() || isSubmitting}
              onClick={handleFinishWizard}
            >
              {t.wizard.createCampaignSubmitBtn}
            </button>
          )}
        </div>
      </footer>
    </div>
  );
};
