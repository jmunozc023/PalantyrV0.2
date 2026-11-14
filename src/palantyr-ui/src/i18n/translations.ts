import {
  EonixState,
  PalantyrLanguage,
  PartyMembershipStatus,
  PauseTranscriptionBehavior,
  ProcessingMode,
  SessionRecordingState,
  SubsystemProcessingState,
  VisibilityScope,
  KnowledgeState,
  ConfidenceLevel,
} from '../types/palantyr';

export type LocaleCode = 'en' | 'es';

export function languageToLocale(lang: PalantyrLanguage): LocaleCode {
  return lang === 'Spanish' ? 'es' : 'en';
}

export interface TranslationDictionary {
  appTitle: string;
  appSubtitle: string;
  offlineBadge: string;
  nav: {
    welcome: string;
    createCampaign: string;
    campaignHome: string;
    settings: string;
    systemAssessment: string;
    simulateCrashRecovery: string;
    attemptWindowClose: string;
    languageToggleLabel: string;
  };
  welcome: {
    heroTitle: string;
    heroSubtitle: string;
    selectCampaignTitle: string;
    createNewButton: string;
    openCampaignButton: string;
    deleteCampaignButton: string;
    lastOpened: string;
    sessionsCount: string;
    partyCount: string;
    needsAttentionBadge: string;
    unbackedUpWarning: string;
    emptyCampaigns: string;
    systemSummaryTitle: string;
    recommendedModeLabel: string;
    rerunAssessmentButton: string;
  };
  sessionStates: Record<SessionRecordingState, string>;
  subsystemStates: Record<SubsystemProcessingState, string>;
  subsystemPatterns: Record<SubsystemProcessingState, string>;
  eonixStates: Record<
    EonixState,
    {
      badge: string;
      statusMessage: string;
      ariaDescription: string;
    }
  >;
  processingModes: Record<
    ProcessingMode,
    {
      title: string;
      description: string;
    }
  >;
  pauseBehaviors: Record<
    PauseTranscriptionBehavior,
    {
      title: string;
      description: string;
    }
  >;
  partyStatuses: Record<PartyMembershipStatus, string>;
  visibilityScopes: Record<
    VisibilityScope,
    {
      label: string;
      description: string;
    }
  >;
  knowledgeStates: Record<KnowledgeState, string>;
  confidenceLevels: Record<ConfidenceLevel, string>;
  subsystems: {
    sectionTitle: string;
    sectionSubtitle: string;
    recordingCardTitle: string;
    recordingCardDesc: string;
    transcriptionCardTitle: string;
    transcriptionCardDesc: string;
    memoryCardTitle: string;
    memoryCardDesc: string;
    eonixCardTitle: string;
    eonixCardDesc: string;
    segmentsSafelyStored: string;
    segmentsTranscribed: string;
    segmentsMemoryProcessed: string;
    freeStorageLabel: string;
    storageWarningBanner: string;
    throttledBannerTitle: string;
    priorityTierNote: string;
  };
  eonixCompanion: {
    name: string;
    title: string;
    privacyBadge: string;
    animationsReducedBadge: string;
    askModeTab: string;
    teachModeTab: string;
    askPlaceholder: string;
    teachPlaceholder: string;
    sendButton: string;
    visibilitySelectorLabel: string;
    quickPromptsTitle: string;
    quickPrompt1: string;
    quickPrompt2: string;
    quickPrompt3: string;
    sampleWelcomeMessage: string;
    sampleAnswerPrefix: string;
    sampleTeachAckPrefix: string;
  };
  campaignHome: {
    workspaceBadge: string;
    activeSessionBadge: string;
    noActiveSession: string;
    elapsedTime: string;
    startRecordingBtn: string;
    pauseRecordingBtn: string;
    resumeRecordingBtn: string;
    endSessionBtn: string;
    pauseAskPromptTitle: string;
    pauseAskPromptBody: string;
    pauseCatchUpYes: string;
    pauseCatchUpNo: string;
    partyRosterPanelTitle: string;
    addQuickMemberBtn: string;
    playerControlled: string;
    dmTemporaryNpc: string;
    lorePanelTitle: string;
    needsAttentionPanelTitle: string;
    needsAttentionDesc: string;
    resolveItemBtn: string;
    markFalsePositiveBtn: string;
    backupStatusSafe: string;
    backupStatusPending: string;
    createBackupNowBtn: string;
  };
  wizard: {
    title: string;
    subtitle: string;
    stepOf: string;
    steps: {
      basics: string;
      world: string;
      roster: string;
      lore: string;
      microphone: string;
      review: string;
    };
    backBtn: string;
    nextBtn: string;
    cancelBtn: string;
    createCampaignSubmitBtn: string;
    basics: {
      campaignNameLabel: string;
      campaignNamePlaceholder: string;
      subtitleLabel: string;
      subtitlePlaceholder: string;
      primaryLanguageLabel: string;
      primaryLanguageHelp: string;
    };
    world: {
      worldSummaryLabel: string;
      worldSummaryPlaceholder: string;
      calendarSystemLabel: string;
      calendarSystemPlaceholder: string;
      calendarHelp: string;
    };
    roster: {
      heading: string;
      subheading: string;
      charNameLabel: string;
      charNamePlaceholder: string;
      raceLabel: string;
      racePlaceholder: string;
      classLabel: string;
      classPlaceholder: string;
      playerLabel: string;
      playerPlaceholder: string;
      memberTypeLabel: string;
      pcOption: string;
      tempNpcOption: string;
      statusLabel: string;
      addMemberBtn: string;
      removeMemberBtn: string;
      emptyRosterNote: string;
    };
    lore: {
      heading: string;
      subheading: string;
      titleLabel: string;
      titlePlaceholder: string;
      categoryLabel: string;
      categoryPlaceholder: string;
      visibilityLabel: string;
      contentLabel: string;
      contentPlaceholder: string;
      addLoreBtn: string;
      removeLoreBtn: string;
      emptyLoreNote: string;
    };
    microphone: {
      heading: string;
      subheading: string;
      selectDeviceLabel: string;
      runSignalTestBtn: string;
      testingSignal: string;
      signalDetectedBadge: string;
      noSignalBadge: string;
      peakLevelLabel: string;
      walProtectionNote: string;
    };
    review: {
      heading: string;
      subheading: string;
      dbIsolationNotice: string;
      summaryBasics: string;
      summaryParty: string;
      summaryLore: string;
      summaryAudio: string;
    };
  };
  modals: {
    firstRun: {
      title: string;
      subtitle: string;
      cpuLabel: string;
      ramLabel: string;
      gpuLabel: string;
      storageLabel: string;
      audioDevicesLabel: string;
      recommendedModeBanner: string;
      selectModeOverrideLabel: string;
      acceptAndContinueBtn: string;
    };
    endSession: {
      stage1Title: string;
      stage1Warning: string;
      stage1Bullet1: string;
      stage1Bullet2: string;
      stage1Bullet3: string;
      stage1CancelBtn: string;
      stage1ConfirmBtn: string;
      stage2Title: string;
      stage2Subtitle: string;
      totalDurationLabel: string;
      safelyStoredLabel: string;
      transcribedLabel: string;
      memoryProcessedLabel: string;
      needsAttentionLabel: string;
      continueProcessingBtn: string;
      finishLaterBtn: string;
      finishLaterNote: string;
    };
    crashRecovery: {
      title: string;
      subtitle: string;
      interruptedAtLabel: string;
      previouslySafeLabel: string;
      recoveredFromWalLabel: string;
      unrecoverableLabel: string;
      recoveredDurationLabel: string;
      walTableTitle: string;
      colSeq: string;
      colSegment: string;
      colDuration: string;
      colStatus: string;
      choiceContinueTitle: string;
      choiceContinueDesc: string;
      choiceEndAndProcessTitle: string;
      choiceEndAndProcessDesc: string;
      choiceCloseAndStartNewTitle: string;
      choiceCloseAndStartNewDesc: string;
    };
    safeDelete: {
      title: string;
      subtitle: string;
      warningText: string;
      confirmNameLabel: string;
      backupAndDeleteBtn: string;
      deleteWithoutBackupBtn: string;
      cancelBtn: string;
    };
    windowCloseGuard: {
      title: string;
      subtitle: string;
      warningBody: string;
      returnToSessionBtn: string;
      endSessionAndExitBtn: string;
    };
    settings: {
      title: string;
      subtitle: string;
      uiLanguageLabel: string;
      processingModeLabel: string;
      pauseBehaviorLabel: string;
      microphoneDeviceLabel: string;
      testMicBtn: string;
      accessibilitySection: string;
      reduceAnimationsLabel: string;
      reduceAnimationsDesc: string;
      storageAndRetentionSection: string;
      keepOriginalAudioLabel: string;
      keepOriginalAudioWarningTitle: string;
      keepOriginalAudioWarningBody: string;
      acknowledgeAudioWarningCheckbox: string;
      rerunAssessmentBtn: string;
      saveSettingsBtn: string;
      closeBtn: string;
    };
  };
}

export const translations: Record<LocaleCode, TranslationDictionary> = {
  en: {
    appTitle: 'PALANTYR',
    appSubtitle: 'Local-First AI Dungeon Master Memory & Session Companion',
    offlineBadge: '100% Local & Offline (Zero Cloud / Zero Telemetry)',
    nav: {
      welcome: 'Campaigns',
      createCampaign: 'New Campaign',
      campaignHome: 'Campaign Home',
      settings: 'Settings',
      systemAssessment: 'Hardware Profile',
      simulateCrashRecovery: 'WAL Recovery Drill',
      attemptWindowClose: 'Test Close Guard',
      languageToggleLabel: 'Language / Idioma',
    },
    welcome: {
      heroTitle: 'Chronicle Every Session Without Losing the Table',
      heroSubtitle:
        'Resilient 15-minute .pseg WAL audio capture, bilingual Whisper transcription, structured canon vs. belief memory, and Eonix — running 100% locally on your Windows rig.',
      selectCampaignTitle: 'Your Isolated Campaign Vaults',
      createNewButton: '+ Guided Create Campaign Wizard',
      openCampaignButton: 'Enter Campaign Home',
      deleteCampaignButton: 'Safe Delete...',
      lastOpened: 'Last opened',
      sessionsCount: 'Sessions',
      partyCount: 'Party Members',
      needsAttentionBadge: 'Needs Attention',
      unbackedUpWarning: 'Unbacked-up changes since last archive',
      emptyCampaigns: 'No campaigns exist yet. Launch the Guided Wizard to forge your first world.',
      systemSummaryTitle: 'Local Hardware & Processing Profile',
      recommendedModeLabel: 'Active Mode',
      rerunAssessmentButton: 'Inspect / Rerun Assessment',
    },
    sessionStates: {
      Idle: 'IDLE — READY FOR SESSION',
      Recording: 'RECORDING LIVE SESSION',
      Paused: 'SESSION PAUSED',
      Ended: 'SESSION ENDED — SAFE ON DISK',
      Recovering: 'RECOVERING INTERRUPTED WAL',
    },
    subsystemStates: {
      Idle: 'Idle',
      Saved: 'Saved',
      Pending: 'Pending',
      Processing: 'Processing',
      Paused: 'Paused',
      Completed: 'Completed',
      Failed: 'Failed',
    },
    subsystemPatterns: {
      Idle: '[ · · · ]',
      Saved: '[ ✓ SAFE ]',
      Pending: '[ ⏳ QUEUED ]',
      Processing: '[ ⚙ ACTIVE ]',
      Paused: '[ ⏸ PAUSED ]',
      Completed: '[ ★ DONE ]',
      Failed: '[ ✖ ALERT ]',
    },
    eonixStates: {
      Idle: {
        badge: 'IDLE — PERCHED & WATCHFUL',
        statusMessage: 'Eonix rests atop the obsidian lectern, ready to chronicle your session or answer private lore queries.',
        ariaDescription: 'Eonix dragon icon in calm resting posture with steady amber eyes.',
      },
      ListeningRecording: {
        badge: 'LISTENING & SAVING AUDIO',
        statusMessage: 'Eonix is actively listening. PCM audio is continuously flushed to the authoritative .pseg WAL.',
        ariaDescription: 'Eonix dragon icon with raised ears and acoustic quill recording live audio.',
      },
      Transcribing: {
        badge: 'TRANSCRIBING SEGMENTS',
        statusMessage: 'Eonix is scribing closed audio segments into timestamped bilingual text.',
        ariaDescription: 'Eonix dragon icon writing on parchment scroll.',
      },
      MemoryProcessing: {
        badge: 'WEAVING STRUCTURED MEMORY',
        statusMessage: 'Eonix is extracting entities, separating In-World Canon from Table Talk, and linking provenance.',
        ariaDescription: 'Eonix dragon icon illuminating crystalline memory facets.',
      },
      Thinking: {
        badge: 'CONSULTING CAMPAIGN ARCHIVES',
        statusMessage: 'Eonix is searching verified transcripts, world lore, and epistemic states...',
        ariaDescription: 'Eonix dragon icon gazing into the palantyr orb.',
      },
      Answering: {
        badge: 'DELIVERING CITED ANSWER',
        statusMessage: 'Eonix has compiled a grounded response with explicit provenance citations.',
        ariaDescription: 'Eonix dragon icon presenting an illuminated scroll.',
      },
      NeedsAttention: {
        badge: 'NEEDS DM ATTENTION',
        statusMessage: 'Eonix flagged ambiguous names, unverified rumors, or low-confidence extractions for your review.',
        ariaDescription: 'Eonix dragon icon holding an amber lantern signaling review items.',
      },
      Paused: {
        badge: 'RECORDING PAUSED',
        statusMessage: 'Session capture is paused. All prior audio segments are sealed safely on disk.',
        ariaDescription: 'Eonix dragon icon with folded wings in paused stance.',
      },
      Error: {
        badge: 'SUBSYSTEM ALERT',
        statusMessage: 'A subsystem reported an issue. Check explicit status cards below — your recorded WAL segments remain safe.',
        ariaDescription: 'Eonix dragon icon with crimson shield warning indicator.',
      },
    },
    processingModes: {
      LiveBalanced: {
        title: 'Live Balanced Mode (GPU Accelerated)',
        description:
          'Captures audio at Tier 1 priority while transcribing completed 15-minute segments in the background when GPU/CPU headroom allows.',
      },
      RecordingOnly: {
        title: 'Recording-Only Mode (Zero AI Overhead During Play)',
        description:
          'Dedicates 100% of resources during the session to rock-solid .pseg/.wav audio capture. Transcription and memory run after End Session.',
      },
      ManualDeferred: {
        title: 'Manual / Deferred Processing Mode',
        description:
          'Captures audio only and never starts transcription or memory extraction until you explicitly trigger processing.',
      },
    },
    pauseBehaviors: {
      Never: {
        title: 'Never Transcribe While Paused',
        description: 'Keep background transcription quiet during session pauses.',
      },
      Ask: {
        title: 'Ask Me When Pausing',
        description: 'Prompt whether to catch up on pending segment transcription during a break.',
      },
      Always: {
        title: 'Always Catch Up While Paused',
        description: 'Automatically use table breaks to transcribe closed audio segments.',
      },
    },
    partyStatuses: {
      Active: 'Active Party Member',
      Temporary: 'Temporary / Guest Companion',
      Former: 'Former Member (Departed)',
      Deceased: 'Deceased (In Memoriam)',
      Inactive: 'Inactive / Absent',
    },
    visibilityScopes: {
      GeneralWorld: {
        label: 'General World (Public Lore)',
        description: 'Common knowledge accessible to anyone in the setting and safe for player-facing summaries.',
      },
      PartyKnown: {
        label: 'Party Known (Shared Canon)',
        description: 'Facts discovered or witnessed by the adventuring party; safe for player-facing recaps.',
      },
      CharacterScoped: {
        label: 'Character-Scoped Knowledge',
        description: 'Known only to specific characters; filtered from general party outputs unless unlocked.',
      },
      DMPrivate: {
        label: 'DM Private (Strict Secret)',
        description: 'Hidden plots, true villain motives, and traps. Hard-filtered before any player summary generation.',
      },
      Ambiguous: {
        label: 'Ambiguous (Pending DM Classification)',
        description: 'Quarantined until the DM confirms visibility scope.',
      },
    },
    knowledgeStates: {
      CampaignTruth: 'Campaign Truth (Objective Canon)',
      DMPrivateTruth: 'DM Private Truth',
      PartyBelief: 'Party Belief (May Be Mistaken)',
      PlayerOrCharacterTheory: 'Player / Character Theory',
      ClaimOrRumor: 'NPC Claim or Tavern Rumor',
      UnknownOrUnresolved: 'Unknown / Unresolved Mystery',
      DisprovenOrSupersededBelief: 'Disproven / Superseded Belief',
    },
    confidenceLevels: {
      DMConfirmed: 'DM Confirmed (100% Authoritative)',
      HighConfidence: 'High Confidence',
      MediumConfidence: 'Medium Confidence',
      LowOrUncertain: 'Low / Needs Review',
    },
    subsystems: {
      sectionTitle: 'Explicit Subsystem Status & Integrity Matrix',
      sectionSubtitle:
        'Each pipeline stage reports independently with explicit text, icon, and pattern badges (AC-USAB-001). Recording is strictly isolated at Priority Tier 1.',
      recordingCardTitle: '1. Recording / Audio Save (.pseg WAL)',
      recordingCardDesc: 'Tier 1 Critical — Continuous 2s PCM flush & 15-min atomic segment rollover',
      transcriptionCardTitle: '2. Speech-to-Text Transcription',
      transcriptionCardDesc: 'Tier 3 Deferrable — Local bilingual Whisper engine on closed segments',
      memoryCardTitle: '3. Structured Memory Extraction',
      memoryCardDesc: 'Tier 3 Deferrable — Canon vs. Table-Talk filtering & entity graph linking',
      eonixCardTitle: '4. Eonix Interactive Query Engine',
      eonixCardDesc: 'Tier 2 High — Private DM Q&A and conversational lore teaching',
      segmentsSafelyStored: 'Segments Safely Stored',
      segmentsTranscribed: 'Segments Transcribed',
      segmentsMemoryProcessed: 'Segments in Memory',
      freeStorageLabel: 'Free Disk Space',
      storageWarningBanner:
        'LOW DISK SPACE WARNING: Free storage is below safe threshold. Audio capture remains protected, but please free disk space soon.',
      throttledBannerTitle: 'Priority Tier 1 Protection Active — Background AI Automatically Throttled:',
      priorityTierNote: 'Audio Recording > Eonix DM Queries > Background Transcription & Memory',
    },
    eonixCompanion: {
      name: 'EONIX',
      title: 'Private DM Dragon Companion & Keeper of the Palantyr',
      privacyBadge: 'DM-Eyes-Only Private Workspace',
      animationsReducedBadge: 'Reduced Motion Active',
      askModeTab: 'Ask Eonix (Cited Q&A)',
      teachModeTab: 'Teach Eonix (Add Direct Canon)',
      askPlaceholder: 'Ask Eonix about NPCs, quests, party beliefs, or session history...',
      teachPlaceholder: 'Teach Eonix a new canon fact, secret plot truth, or correction...',
      sendButton: 'Send to Eonix',
      visibilitySelectorLabel: 'Visibility Scope for New Teaching:',
      quickPromptsTitle: 'Quick DM Queries:',
      quickPrompt1: 'What does the party currently believe about the Obsidian Spire?',
      quickPrompt2: 'Which NPCs were introduced in Session 1 and what is their status?',
      quickPrompt3: 'Show DM-private secrets vs. what the players know.',
      sampleWelcomeMessage:
        'Greetings, Dungeon Master. All campaign records are locked in your local SQLite vault. Ask me anything about your world, or switch to "Teach Eonix" to inscribe new canon.',
      sampleAnswerPrefix: '[Grounded Archive Response • Cited from Campaign Lore & Session Transcripts]:',
      sampleTeachAckPrefix: '[Canon Inscribed • Provenance: DMConversationalTeaching • Confidence: DMConfirmed]:',
    },
    campaignHome: {
      workspaceBadge: 'PERSISTENT CAMPAIGN HOME WORKSPACE',
      activeSessionBadge: 'Active Session ID',
      noActiveSession: 'No active session recording — Workspace in Idle preparation mode',
      elapsedTime: 'Session Duration',
      startRecordingBtn: '● Start Session Recording',
      pauseRecordingBtn: '⏸ Pause Recording',
      resumeRecordingBtn: '▶ Resume Recording',
      endSessionBtn: '■ End Session (Two-Stage Safe Close)',
      pauseAskPromptTitle: 'Session Paused — Run Background Transcription During Break?',
      pauseAskPromptBody:
        'Your Pause Preference is set to "Ask". Would you like Palantyr to transcribe already-saved audio segments while recording is paused?',
      pauseCatchUpYes: 'Yes, Transcribe Saved Segments Now',
      pauseCatchUpNo: 'No, Keep Background Quiet',
      partyRosterPanelTitle: 'Party Roster & Historical Membership',
      addQuickMemberBtn: '+ Update Roster',
      playerControlled: 'Player',
      dmTemporaryNpc: 'Temporary Companion / NPC',
      lorePanelTitle: 'World Lore & Epistemic Vault',
      needsAttentionPanelTitle: 'Needs Attention Queue (Low-Confidence / Ambiguous)',
      needsAttentionDesc:
        'Uncertain extractions are quarantined here and never silently merged into Campaign Truth.',
      resolveItemBtn: 'Confirm as Canon',
      markFalsePositiveBtn: 'Dismiss (Table Talk / Joke)',
      backupStatusSafe: 'Vault Backed Up',
      backupStatusPending: 'Unbacked-Up Changes',
      createBackupNowBtn: 'Create .palantyr-backup',
    },
    wizard: {
      title: 'Guided Create Campaign Wizard',
      subtitle:
        'Provisions an isolated per-campaign SQLite database (`campaign_{guid}.db`) and dedicated audio/backup directories.',
      stepOf: 'Step',
      steps: {
        basics: '1. Basics & Language',
        world: '2. World & Calendar',
        roster: '3. Initial Party Roster',
        lore: '4. Initial World Lore',
        microphone: '5. Microphone Check',
        review: '6. Review & Provision',
      },
      backBtn: '← Previous Step',
      nextBtn: 'Next Step →',
      cancelBtn: 'Cancel & Return',
      createCampaignSubmitBtn: '✦ Forge Campaign & Enter Campaign Home',
      basics: {
        campaignNameLabel: 'Campaign Name *',
        campaignNamePlaceholder: 'e.g., Ashes of the Shattered Crown',
        subtitleLabel: 'Subtitle or Setting Name',
        subtitlePlaceholder: 'e.g., High Fantasy Homebrew — Continent of Valen',
        primaryLanguageLabel: 'Primary Campaign & Transcription Language *',
        primaryLanguageHelp:
          'Configures the local Whisper speech-to-text model and entity extraction prompts for your table.',
      },
      world: {
        worldSummaryLabel: 'World Overview & Core Premise',
        worldSummaryPlaceholder:
          'Describe the central tension, major regions, or starting hook for your campaign...',
        calendarSystemLabel: 'In-World Calendar System (Optional)',
        calendarSystemPlaceholder: 'e.g., Imperial Solar Reckoning (12 months of 30 days, current year 842 AE)',
        calendarHelp: 'Helps Eonix anchor relative timeline references ("three days after the siege").',
      },
      roster: {
        heading: 'Initial Adventuring Party & Companions',
        subheading:
          'Register player characters (PCs) and any starting temporary NPCs. Full membership history (Active, Temporary, Former, Deceased, Inactive) is tracked across sessions.',
        charNameLabel: 'Character Name *',
        charNamePlaceholder: 'e.g., Kaelen Dawnstrider',
        raceLabel: 'Race / Species',
        racePlaceholder: 'e.g., Half-Elf',
        classLabel: 'Class / Archetype',
        classPlaceholder: 'e.g., Oath of Ancients Paladin',
        playerLabel: 'Controlled by Player Name',
        playerPlaceholder: 'e.g., Sofía (leave blank for DM NPC)',
        memberTypeLabel: 'Character Role',
        pcOption: 'Player Character (PC)',
        tempNpcOption: 'Temporary Companion / DM NPC',
        statusLabel: 'Initial Status',
        addMemberBtn: '+ Add to Party Roster',
        removeMemberBtn: 'Remove',
        emptyRosterNote: 'No characters added yet. You can add party members now or anytime from Campaign Home.',
      },
      lore: {
        heading: 'Initial World Knowledge & Secrets',
        subheading:
          'Seed your campaign with foundational lore. Every entry carries an explicit Visibility Scope so DM Private truths never leak into player summaries.',
        titleLabel: 'Lore Entry Title *',
        titlePlaceholder: 'e.g., The Concordat of Silver Spires',
        categoryLabel: 'Category',
        categoryPlaceholder: 'e.g., Faction / History / Villain Secret',
        visibilityLabel: 'Privacy & Visibility Classification *',
        contentLabel: 'Lore Description & Facts *',
        contentPlaceholder: 'Enter world canon, party-known context, or strictly private DM truths...',
        addLoreBtn: '+ Add World Lore Entry',
        removeLoreBtn: 'Remove',
        emptyLoreNote: 'No initial lore entries added yet.',
      },
      microphone: {
        heading: 'Microphone Selection & Live Signal Verification',
        subheading:
          'Verify your table microphone before your first session. Palantyr validates device availability and detectable acoustic signal (FR-REC-001).',
        selectDeviceLabel: 'Preferred Audio Input Device',
        runSignalTestBtn: '🎙 Run Live Signal Validation',
        testingSignal: 'Sampling PCM Input...',
        signalDetectedBadge: '✓ Detectable Signal Verified',
        noSignalBadge: '⚠ No Acoustic Signal Detected',
        peakLevelLabel: 'Measured Peak Level',
        walProtectionNote:
          'All live recordings use an authoritative filesystem Write-Ahead Log (`session_recording.pseg`) with 2-second flushes and automatic crash recovery.',
      },
      review: {
        heading: 'Final Review & Isolated Vault Provisioning',
        subheading:
          'Confirm your campaign configuration below. Creating the campaign initializes a dedicated SQLite database with zero cross-campaign data bleed.',
        dbIsolationNotice: 'Isolated SQLite Vault:',
        summaryBasics: 'Campaign Identity & Language',
        summaryParty: 'Initial Party Roster',
        summaryLore: 'Seeded World Knowledge',
        summaryAudio: 'Configured Audio Capture Device',
      },
    },
    modals: {
      firstRun: {
        title: 'First-Run Local Hardware & System Assessment',
        subtitle:
          'Palantyr inspected your local Windows hardware to recommend the safest processing mode that guarantees zero audio dropouts (FR-SYS-001..005).',
        cpuLabel: 'Processor (CPU)',
        ramLabel: 'System Memory (RAM)',
        gpuLabel: 'Graphics / AI Accelerator (GPU)',
        storageLabel: 'Available Local Storage',
        audioDevicesLabel: 'Detected Audio Input Devices',
        recommendedModeBanner: 'Recommended Processing Mode for Your Hardware:',
        selectModeOverrideLabel: 'Confirm or Override Processing Mode:',
        acceptAndContinueBtn: 'Save Profile & Continue to Palantyr',
      },
      endSession: {
        stage1Title: 'Stage 1 of 2 — Confirm End of Today’s Session',
        stage1Warning:
          'Are you ready to conclude active audio recording for this session and seal the final .pseg WAL segment?',
        stage1Bullet1: 'Active microphone capture will stop immediately and flush remaining PCM frames.',
        stage1Bullet2:
          'The final audio segment will be atomically sealed (`ClosedSafe`) inside `session_recording.pseg`.',
        stage1Bullet3:
          'You will remain inside Campaign Home and choose whether to finish background transcription now or defer it for later.',
        stage1CancelBtn: 'Keep Recording Session',
        stage1ConfirmBtn: 'Seal Final Audio Segment & End Recording →',
        stage2Title: 'Stage 2 of 2 — Audio Safely Stored on Disk',
        stage2Subtitle:
          'All recorded audio segments for this session are sealed and verified. How would you like to handle remaining background AI processing?',
        totalDurationLabel: 'Total Recorded Duration',
        safelyStoredLabel: 'Safely Stored Audio Segments',
        transcribedLabel: 'Transcribed Segments',
        memoryProcessedLabel: 'Memory-Processed Segments',
        needsAttentionLabel: 'Items Flagged for DM Review',
        continueProcessingBtn: '⚙ Continue Background Processing Now',
        finishLaterBtn: '🌙 Pause Processing & Finish Later',
        finishLaterNote:
          'Choosing "Finish Later" leaves all sealed audio segments safely queued so you can close Palantyr immediately.',
      },
      crashRecovery: {
        title: 'Interrupted Session Detected — Authoritative .pseg WAL Recovery',
        subtitle:
          'Palantyr detected an unclosed session from a previous power loss or unexpected exit. The filesystem Write-Ahead Log (`session_recording.pseg`) has been reconciled against raw `.wav` segments (FR-REC-006..008).',
        interruptedAtLabel: 'Interruption Timestamp',
        previouslySafeLabel: 'Previously Sealed Segments',
        recoveredFromWalLabel: 'Segments Reconciled & Recovered from WAL',
        unrecoverableLabel: 'Corrupted / Unrecoverable Segments',
        recoveredDurationLabel: 'Total Preserved Session Audio',
        walTableTitle: 'Segment-Level WAL Integrity Ledger',
        colSeq: 'Seq #',
        colSegment: 'Segment File',
        colDuration: 'Duration',
        colStatus: 'WAL Status',
        choiceContinueTitle: '1. Continue Interrupted Session',
        choiceContinueDesc: 'Re-open the recovered session and start a new sequential audio segment.',
        choiceEndAndProcessTitle: '2. End Session & Process Recovered Audio',
        choiceEndAndProcessDesc:
          'Mark the recovered session as completed and queue its segments for transcription & memory extraction.',
        choiceCloseAndStartNewTitle: '3. Close Safely & Start Fresh Session',
        choiceCloseAndStartNewDesc:
          'Seal the recovered segments in the archive for later and return to an Idle Campaign Home.',
      },
      safeDelete: {
        title: 'Safe Campaign Deletion Protocol',
        subtitle:
          'Deleting a campaign removes its isolated SQLite database and session audio folder. Palantyr strongly recommends creating a portable `.palantyr-backup` archive first (AC-DATA-006).',
        warningText:
          'Select how you want to proceed. "Create Backup and Delete" exports a verified snapshot before removing the campaign.',
        confirmNameLabel: 'Campaign selected for deletion:',
        backupAndDeleteBtn: '🛡 Create Backup Archive & Delete Campaign (Recommended)',
        deleteWithoutBackupBtn: '⚠ Delete Permanently Without Backup',
        cancelBtn: 'Cancel — Keep Campaign Safe',
      },
      windowCloseGuard: {
        title: 'Active Session Recording in Progress!',
        subtitle:
          'Window Close Protection Guard triggered (`NFR-REL-009`, `AC-REL-004`). Closing the application abruptly while recording is discouraged.',
        warningBody:
          'Palantyr is actively capturing table audio. Would you like to safely seal the current audio segment and end the session before exiting, or return to your active session?',
        returnToSessionBtn: '← Return to Active Session',
        endSessionAndExitBtn: '■ Safely End Session & Exit',
      },
      settings: {
        title: 'Palantyr Settings & Local Stewardship',
        subtitle: 'Configure bilingual UI, AI processing behavior, accessibility, and raw audio retention.',
        uiLanguageLabel: 'Interface & Prompt Language (EN / ES)',
        processingModeLabel: 'Active AI Processing Mode',
        pauseBehaviorLabel: 'When Session Recording is Paused (FR-PROC-003)',
        microphoneDeviceLabel: 'Default Audio Input Device',
        testMicBtn: 'Test Signal',
        accessibilitySection: 'Visual Comfort & Accessibility (NFR-USAB-009)',
        reduceAnimationsLabel: 'Reduce Eonix & UI Animations',
        reduceAnimationsDesc:
          'Replaces continuous dragon bobbing, pulsing rings, and transitions with calm static indicators (AC-USAB-004).',
        storageAndRetentionSection: 'Audio Retention & Disk Space Policy (NFR-DATA-006)',
        keepOriginalAudioLabel: 'Keep Original Session Audio (.wav) After Full Processing',
        keepOriginalAudioWarningTitle: 'MANDATORY DISK SPACE WARNING (NFR-DATA-006 / AC-DATA-004):',
        keepOriginalAudioWarningBody:
          'By default, Palantyr deletes raw .wav segments only after transcription AND structured memory extraction have both succeeded. Enabling "Keep Original Session Audio" preserves uncompressed PCM .wav files indefinitely (~320 MB to 650 MB per 4-hour session), which can rapidly consume gigabytes of local SSD storage.',
        acknowledgeAudioWarningCheckbox:
          'I understand the local disk space impact and wish to retain original .wav audio files indefinitely.',
        rerunAssessmentBtn: '⟳ Rerun Hardware System Assessment',
        saveSettingsBtn: 'Save Settings',
        closeBtn: 'Close',
      },
    },
  },
  es: {
    appTitle: 'PALANTYR',
    appSubtitle: 'Compañero Local de IA y Memoria de Campaña para Dungeon Masters',
    offlineBadge: '100% Local y Sin Conexión (Cero Nube / Cero Telemetría)',
    nav: {
      welcome: 'Campañas',
      createCampaign: 'Nueva Campaña',
      campaignHome: 'Hogar de Campaña',
      settings: 'Ajustes',
      systemAssessment: 'Perfil de Hardware',
      simulateCrashRecovery: 'Simular Recuperación WAL',
      attemptWindowClose: 'Probar Guardia de Cierre',
      languageToggleLabel: 'Idioma / Language',
    },
    welcome: {
      heroTitle: 'Registra Cada Sesión Sin Perder el Ritmo de la Mesa',
      heroSubtitle:
        'Captura de audio resiliente en segmentos .pseg WAL de 15 minutos, transcripción bilingüe Whisper, memoria estructurada que separa canon de rumores, y Eonix — ejecutándose 100% localmente en tu equipo Windows.',
      selectCampaignTitle: 'Tus Bóvedas de Campaña Aisladas',
      createNewButton: '+ Asistente Guiado para Crear Campaña',
      openCampaignButton: 'Entrar al Hogar de Campaña',
      deleteCampaignButton: 'Eliminación Segura...',
      lastOpened: 'Última apertura',
      sessionsCount: 'Sesiones',
      partyCount: 'Miembros del Grupo',
      needsAttentionBadge: 'Requiere Atención',
      unbackedUpWarning: 'Cambios sin respaldo desde el último archivo',
      emptyCampaigns: 'Aún no existen campañas. Inicia el Asistente Guiado para forjar tu primer mundo.',
      systemSummaryTitle: 'Perfil de Hardware Local y Procesamiento',
      recommendedModeLabel: 'Modo Activo',
      rerunAssessmentButton: 'Inspeccionar / Reevaluar Equipo',
    },
    sessionStates: {
      Idle: 'INACTIVO — LISTO PARA LA SESIÓN',
      Recording: 'GRABANDO SESIÓN EN VIVO',
      Paused: 'SESIÓN EN PAUSA',
      Ended: 'SESIÓN FINALIZADA — SEGURA EN DISCO',
      Recovering: 'RECUPERANDO WAL INTERRUMPIDO',
    },
    subsystemStates: {
      Idle: 'Inactivo',
      Saved: 'Guardado',
      Pending: 'Pendiente',
      Processing: 'Procesando',
      Paused: 'Pausado',
      Completed: 'Completado',
      Failed: 'Fallido',
    },
    subsystemPatterns: {
      Idle: '[ · · · ]',
      Saved: '[ ✓ SEGURO ]',
      Pending: '[ ⏳ EN COLA ]',
      Processing: '[ ⚙ ACTIVO ]',
      Paused: '[ ⏸ PAUSADO ]',
      Completed: '[ ★ LISTO ]',
      Failed: '[ ✖ ALERTA ]',
    },
    eonixStates: {
      Idle: {
        badge: 'INACTIVO — ATENTO EN SU ATRIL',
        statusMessage:
          'Eonix descansa sobre el atril de obsidiana, listo para registrar tu sesión o responder consultas privadas del DM.',
        ariaDescription: 'Icono del dragón Eonix en postura tranquila con ojos ámbar estables.',
      },
      ListeningRecording: {
        badge: 'ESCUCHANDO Y GUARDANDO AUDIO',
        statusMessage:
          'Eonix escucha activamente. El audio PCM se vuelca continuamente al registro WAL autoritativo .pseg.',
        ariaDescription: 'Icono del dragón Eonix con orejas atentas y pluma acústica grabando audio en vivo.',
      },
      Transcribing: {
        badge: 'TRANSCRIBIENDO SEGMENTOS',
        statusMessage: 'Eonix está transcribiendo los segmentos de audio cerrados en texto bilingüe con marcas de tiempo.',
        ariaDescription: 'Icono del dragón Eonix escribiendo sobre un pergamino.',
      },
      MemoryProcessing: {
        badge: 'TEJIENDO MEMORIA ESTRUCTURADA',
        statusMessage:
          'Eonix extrae entidades, separa el Canon del Mundo de las charlas fuera de personaje y vincula procedencias.',
        ariaDescription: 'Icono del dragón Eonix iluminando facetas cristalinas de memoria.',
      },
      Thinking: {
        badge: 'CONSULTANDO ARCHIVOS DE CAMPAÑA',
        statusMessage: 'Eonix busca en transcripciones verificadas, historia del mundo y estados epistémicos...',
        ariaDescription: 'Icono del dragón Eonix observando el orbe palantyr.',
      },
      Answering: {
        badge: 'ENTREGANDO RESPUESTA CITADA',
        statusMessage: 'Eonix ha preparado una respuesta fundamentada con citas explícitas de procedencia.',
        ariaDescription: 'Icono del dragón Eonix presentando un pergamino iluminado.',
      },
      NeedsAttention: {
        badge: 'REQUIERE ATENCIÓN DEL DM',
        statusMessage:
          'Eonix marcó nombres ambiguos, rumores no verificados o extracciones de baja confianza para tu revisión.',
        ariaDescription: 'Icono del dragón Eonix sosteniendo una linterna ámbar de revisión.',
      },
      Paused: {
        badge: 'GRABACIÓN EN PAUSA',
        statusMessage: 'La captura de sesión está pausada. Todos los segmentos previos están sellados en disco.',
        ariaDescription: 'Icono del dragón Eonix con alas plegadas en postura de pausa.',
      },
      Error: {
        badge: 'ALERTA DE SUBSISTEMA',
        statusMessage:
          'Un subsistema reportó una incidencia. Revisa las tarjetas explícitas abajo — tus segmentos WAL siguen a salvo.',
        ariaDescription: 'Icono del dragón Eonix con escudo carmesí de advertencia.',
      },
    },
    processingModes: {
      LiveBalanced: {
        title: 'Modo Equilibrado en Vivo (Acelerado por GPU)',
        description:
          'Captura audio con prioridad de Nivel 1 mientras transcribe en segundo plano los segmentos cerrados de 15 min cuando hay margen de GPU/CPU.',
      },
      RecordingOnly: {
        title: 'Modo Solo Grabación (Cero Carga de IA Durante la Partida)',
        description:
          'Dedica el 100% de los recursos durante la sesión a la captura .pseg/.wav. La transcripción y memoria se ejecutan al finalizar la sesión.',
      },
      ManualDeferred: {
        title: 'Modo Manual / Procesamiento Diferido',
        description:
          'Solo captura audio y nunca inicia la transcripción ni la extracción de memoria hasta que lo ordenes manualmente.',
      },
    },
    pauseBehaviors: {
      Never: {
        title: 'Nunca Transcribir en Pausa',
        description: 'Mantiene la transcripción en segundo plano detenida durante las pausas.',
      },
      Ask: {
        title: 'Preguntarme al Pausar',
        description: 'Pregunta si deseas adelantar la transcripción de segmentos guardados durante el descanso.',
      },
      Always: {
        title: 'Adelantar Siempre en Pausa',
        description: 'Aprovecha automáticamente los descansos de la mesa para transcribir segmentos cerrados.',
      },
    },
    partyStatuses: {
      Active: 'Miembro Activo del Grupo',
      Temporary: 'Compañero Temporal / Invitado',
      Former: 'Antiguo Miembro (Retirado)',
      Deceased: 'Fallecido (En Memoria)',
      Inactive: 'Inactivo / Ausente',
    },
    visibilityScopes: {
      GeneralWorld: {
        label: 'Mundo General (Saber Público)',
        description: 'Conocimiento común en el mundo; seguro para incluir en resúmenes para jugadores.',
      },
      PartyKnown: {
        label: 'Conocido por el Grupo (Canon Compartido)',
        description: 'Hechos descubiertos o presenciados por los aventureros; seguro para resúmenes de jugadores.',
      },
      CharacterScoped: {
        label: 'Conocimiento de Personaje Específico',
        description: 'Conocido solo por ciertos personajes; filtrado de resúmenes generales salvo autorización.',
      },
      DMPrivate: {
        label: 'Privado del DM (Secreto Estricto)',
        description:
          'Tramas ocultas, motivos reales de villanos y trampas. Filtrado estrictamente antes de generar cualquier resumen para jugadores.',
      },
      Ambiguous: {
        label: 'Ambiguo (Pendiente de Clasificación del DM)',
        description: 'En cuarentena hasta que el DM confirme su alcance de visibilidad.',
      },
    },
    knowledgeStates: {
      CampaignTruth: 'Verdad de Campaña (Canon Objetivo)',
      DMPrivateTruth: 'Verdad Privada del DM',
      PartyBelief: 'Creencia del Grupo (Puede Estar Equivocada)',
      PlayerOrCharacterTheory: 'Teoría de Jugador / Personaje',
      ClaimOrRumor: 'Afirmación de PNJ o Rumor de Taberna',
      UnknownOrUnresolved: 'Misterio Desconocido / Sin Resolver',
      DisprovenOrSupersededBelief: 'Creencia Desmentida / Superada',
    },
    confidenceLevels: {
      DMConfirmed: 'Confirmado por el DM (100% Autoritativo)',
      HighConfidence: 'Confianza Alta',
      MediumConfidence: 'Confianza Media',
      LowOrUncertain: 'Baja / Requiere Revisión',
    },
    subsystems: {
      sectionTitle: 'Matriz Explícita de Estado e Integridad de Subsistemas',
      sectionSubtitle:
        'Cada etapa reporta de forma independiente con etiquetas de texto explícitas, iconos y patrones (AC-USAB-001). La grabación está protegida en Nivel 1.',
      recordingCardTitle: '1. Grabación / Guardado de Audio (WAL .pseg)',
      recordingCardDesc: 'Nivel 1 Crítico — Volcado PCM cada 2s y rotación atómica de segmentos de 15 min',
      transcriptionCardTitle: '2. Transcripción de Voz a Texto',
      transcriptionCardDesc: 'Nivel 3 Diferible — Motor Whisper local bilingüe sobre segmentos cerrados',
      memoryCardTitle: '3. Extracción de Memoria Estructurada',
      memoryCardDesc: 'Nivel 3 Diferible — Filtro de Canon vs. Charla de Mesa y grafo de entidades',
      eonixCardTitle: '4. Motor de Consultas Interactivas Eonix',
      eonixCardDesc: 'Nivel 2 Alto — Preguntas privadas del DM y enseñanza conversacional de canon',
      segmentsSafelyStored: 'Segmentos Guardados Seguros',
      segmentsTranscribed: 'Segmentos Transcritos',
      segmentsMemoryProcessed: 'Segmentos en Memoria',
      freeStorageLabel: 'Espacio Libre en Disco',
      storageWarningBanner:
        'ADVERTENCIA DE ESPACIO EN DISCO: El almacenamiento libre está bajo el umbral recomendado. Tu grabación sigue protegida, pero libera espacio pronto.',
      throttledBannerTitle: 'Protección de Nivel 1 Activa — IA en Segundo Plano Regulada Automáticamente:',
      priorityTierNote: 'Grabación de Audio > Consultas a Eonix > Transcripción y Memoria en Segundo Plano',
    },
    eonixCompanion: {
      name: 'EONIX',
      title: 'Compañero Dragón Privado del DM y Guardián del Palantyr',
      privacyBadge: 'Espacio Privado Exclusivo del DM',
      animationsReducedBadge: 'Movimiento Reducido Activo',
      askModeTab: 'Preguntar a Eonix (Citas Verificadas)',
      teachModeTab: 'Enseñar a Eonix (Añadir Canon Directo)',
      askPlaceholder: 'Pregunta a Eonix sobre PNJs, misiones, creencias del grupo o historial de sesiones...',
      teachPlaceholder: 'Enseña a Eonix un nuevo hecho canónico, secreto del DM o corrección...',
      sendButton: 'Enviar a Eonix',
      visibilitySelectorLabel: 'Visibilidad para la Nueva Enseñanza:',
      quickPromptsTitle: 'Consultas Rápidas del DM:',
      quickPrompt1: '¿Qué cree actualmente el grupo sobre la Aguja de Obsidiana?',
      quickPrompt2: '¿Qué PNJs aparecieron en la Sesión 1 y cuál es su estado?',
      quickPrompt3: 'Muestra los secretos privados del DM frente a lo que saben los jugadores.',
      sampleWelcomeMessage:
        'Saludos, Dungeon Master. Todos los registros están resguardados en tu bóveda SQLite local. Pregúntame cualquier detalle de tu mundo o cambia a "Enseñar a Eonix" para inscribir nuevo canon.',
      sampleAnswerPrefix: '[Respuesta Fundamentada • Citada del Lore de Campaña y Transcripciones]:',
      sampleTeachAckPrefix: '[Canon Inscrito • Procedencia: DMConversationalTeaching • Confianza: DMConfirmed]:',
    },
    campaignHome: {
      workspaceBadge: 'ESPACIO PERSISTENTE DE HOGAR DE CAMPAÑA',
      activeSessionBadge: 'ID de Sesión Activa',
      noActiveSession: 'Sin grabación activa — Espacio en modo de preparación Inactivo',
      elapsedTime: 'Duración de Sesión',
      startRecordingBtn: '● Iniciar Grabación de Sesión',
      pauseRecordingBtn: '⏸ Pausar Grabación',
      resumeRecordingBtn: '▶ Reanudar Grabación',
      endSessionBtn: '■ Finalizar Sesión (Cierre Seguro en 2 Etapas)',
      pauseAskPromptTitle: 'Sesión Pausada — ¿Ejecutar Transcripción Durante el Descanso?',
      pauseAskPromptBody:
        'Tu preferencia de pausa está en "Preguntar". ¿Deseas que Palantyr transcriba los segmentos ya guardados mientras la grabación está en pausa?',
      pauseCatchUpYes: 'Sí, Transcribir Segmentos Guardados Ahora',
      pauseCatchUpNo: 'No, Mantener Segundo Plano en Silencio',
      partyRosterPanelTitle: 'Grupo de Aventureros e Historial de Membresía',
      addQuickMemberBtn: '+ Actualizar Grupo',
      playerControlled: 'Jugador(a)',
      dmTemporaryNpc: 'Compañero Temporal / PNJ',
      lorePanelTitle: 'Lore del Mundo y Bóveda Epistémica',
      needsAttentionPanelTitle: 'Cola de Atención del DM (Baja Confianza / Ambiguos)',
      needsAttentionDesc:
        'Las extracciones inciertas quedan en cuarentena aquí y nunca se mezclan silenciosamente con la Verdad de Campaña.',
      resolveItemBtn: 'Confirmar como Canon',
      markFalsePositiveBtn: 'Descartar (Broma / Charla de Mesa)',
      backupStatusSafe: 'Bóveda Respaldada',
      backupStatusPending: 'Cambios Sin Respaldo',
      createBackupNowBtn: 'Crear .palantyr-backup',
    },
    wizard: {
      title: 'Asistente Guiado para Crear Campaña',
      subtitle:
        'Aprovisiona una base de datos SQLite aislada por campaña (`campaign_{guid}.db`) y directorios dedicados de audio y copias de seguridad.',
      stepOf: 'Paso',
      steps: {
        basics: '1. Datos Básicos e Idioma',
        world: '2. Mundo y Calendario',
        roster: '3. Grupo Inicial',
        lore: '4. Conocimiento Inicial',
        microphone: '5. Prueba de Micrófono',
        review: '6. Revisión y Creación',
      },
      backBtn: '← Paso Anterior',
      nextBtn: 'Siguiente Paso →',
      cancelBtn: 'Cancelar y Volver',
      createCampaignSubmitBtn: '✦ Forjar Campaña y Entrar al Hogar de Campaña',
      basics: {
        campaignNameLabel: 'Nombre de la Campaña *',
        campaignNamePlaceholder: 'p. ej., Cenizas de la Corona Quebrada',
        subtitleLabel: 'Subtítulo o Ambientación',
        subtitlePlaceholder: 'p. ej., Fantasía Heroica Propia — Continente de Valen',
        primaryLanguageLabel: 'Idioma Principal de la Campaña y Transcripción *',
        primaryLanguageHelp:
          'Configura el modelo local Whisper de voz a texto y las plantillas de extracción de entidades para tu mesa.',
      },
      world: {
        worldSummaryLabel: 'Resumen del Mundo y Premisa Central',
        worldSummaryPlaceholder:
          'Describe el conflicto principal, las regiones clave o el punto de partida de tu campaña...',
        calendarSystemLabel: 'Sistema de Calendario del Mundo (Opcional)',
        calendarSystemPlaceholder: 'p. ej., Cómputo Solar Imperial (12 meses de 30 días, año actual 842 DE)',
        calendarHelp: 'Ayuda a Eonix a ubicar referencias temporales relativas ("tres días después del asedio").',
      },
      roster: {
        heading: 'Grupo Inicial de Aventureros y Compañeros',
        subheading:
          'Registra los personajes jugadores (PJ) y cualquier PNJ temporal inicial. Se conserva el historial completo de estados (Activo, Temporal, Antiguo, Fallecido, Inactivo).',
        charNameLabel: 'Nombre del Personaje *',
        charNamePlaceholder: 'p. ej., Kaelen Caminante del Alba',
        raceLabel: 'Raza / Especie',
        racePlaceholder: 'p. ej., Semielfo',
        classLabel: 'Clase / Arquetipo',
        classPlaceholder: 'p. ej., Paladín del Juramento de los Antiguos',
        playerLabel: 'Controlado por el Jugador(a)',
        playerPlaceholder: 'p. ej., Sofía (dejar vacío para PNJ del DM)',
        memberTypeLabel: 'Rol en el Grupo',
        pcOption: 'Personaje Jugador (PJ)',
        tempNpcOption: 'Compañero Temporal / PNJ del DM',
        statusLabel: 'Estado Inicial',
        addMemberBtn: '+ Añadir al Grupo',
        removeMemberBtn: 'Quitar',
        emptyRosterNote:
          'Aún no se han añadido personajes. Puedes añadirlos ahora o en cualquier momento desde el Hogar de Campaña.',
      },
      lore: {
        heading: 'Conocimiento Inicial del Mundo y Secretos',
        subheading:
          'Siembra el lore fundamental de tu campaña. Cada entrada incluye un Alcance de Visibilidad explícito para que los secretos del DM jamás se filtren a los jugadores.',
        titleLabel: 'Título de la Entrada de Lore *',
        titlePlaceholder: 'p. ej., El Concordato de las Agujas de Plata',
        categoryLabel: 'Categoría',
        categoryPlaceholder: 'p. ej., Facción / Historia / Secreto de Villano',
        visibilityLabel: 'Clasificación de Privacidad y Visibilidad *',
        contentLabel: 'Descripción y Hechos *',
        contentPlaceholder:
          'Escribe hechos canónicos del mundo, conocimiento del grupo o verdades estrictamente privadas del DM...',
        addLoreBtn: '+ Añadir Entrada de Lore',
        removeLoreBtn: 'Quitar',
        emptyLoreNote: 'Aún no se han añadido entradas de lore inicial.',
      },
      microphone: {
        heading: 'Selección de Micrófono y Prueba de Señal en Vivo',
        subheading:
          'Verifica el micrófono de tu mesa antes de la primera sesión. Palantyr valida la disponibilidad del dispositivo y la presencia de señal acústica detectable (FR-REC-001).',
        selectDeviceLabel: 'Dispositivo de Entrada de Audio Preferido',
        runSignalTestBtn: '🎙 Ejecutar Validación de Señal en Vivo',
        testingSignal: 'Muestreando entrada PCM...',
        signalDetectedBadge: '✓ Señal Acústica Detectable Verificada',
        noSignalBadge: '⚠ Sin Señal Acústica Detectada',
        peakLevelLabel: 'Nivel Pico Medido',
        walProtectionNote:
          'Todas las grabaciones utilizan un registro Write-Ahead Log autoritativo en disco (`session_recording.pseg`) con volcados cada 2 segundos y recuperación automática ante fallos.',
      },
      review: {
        heading: 'Revisión Final y Aprovisionamiento de Bóveda Aislada',
        subheading:
          'Confirma la configuración de tu campaña. Al crearla se inicializa una base de datos SQLite exclusiva sin mezcla de datos entre campañas.',
        dbIsolationNotice: 'Bóveda SQLite Aislada:',
        summaryBasics: 'Identidad e Idioma de la Campaña',
        summaryParty: 'Grupo Inicial de Aventureros',
        summaryLore: 'Conocimiento Inicial del Mundo',
        summaryAudio: 'Dispositivo de Captura Configurado',
      },
    },
    modals: {
      firstRun: {
        title: 'Evaluación Inicial de Hardware y Sistema Local',
        subtitle:
          'Palantyr analizó tu hardware Windows local para recomendar el modo de procesamiento más seguro que garantice cero pérdidas de audio (FR-SYS-001..005).',
        cpuLabel: 'Procesador (CPU)',
        ramLabel: 'Memoria del Sistema (RAM)',
        gpuLabel: 'Acelerador Gráfico / IA (GPU)',
        storageLabel: 'Almacenamiento Local Disponible',
        audioDevicesLabel: 'Dispositivos de Entrada de Audio Detectados',
        recommendedModeBanner: 'Modo de Procesamiento Recomendado para tu Equipo:',
        selectModeOverrideLabel: 'Confirmar o Cambiar Modo de Procesamiento:',
        acceptAndContinueBtn: 'Guardar Perfil y Continuar a Palantyr',
      },
      endSession: {
        stage1Title: 'Etapa 1 de 2 — Confirmar Cierre de la Sesión de Hoy',
        stage1Warning:
          '¿Estás listo para concluir la grabación de audio activa de esta sesión y sellar el segmento final en el registro WAL .pseg?',
        stage1Bullet1: 'La captura activa del micrófono se detendrá de inmediato volcando los cuadros PCM restantes.',
        stage1Bullet2:
          'El último segmento de audio se sellará atómicamente (`ClosedSafe`) dentro de `session_recording.pseg`.',
        stage1Bullet3:
          'Permanecerás dentro del Hogar de Campaña y podrás elegir si deseas completar el procesamiento ahora o dejarlo para después.',
        stage1CancelBtn: 'Seguir Grabando la Sesión',
        stage1ConfirmBtn: 'Sellar Segmento Final y Terminar Grabación →',
        stage2Title: 'Etapa 2 de 2 — Audio Guardado de Forma Segura en Disco',
        stage2Subtitle:
          'Todos los segmentos de audio grabados están sellados y verificados. ¿Cómo deseas gestionar el procesamiento pendiente de IA?',
        totalDurationLabel: 'Duración Total Grabada',
        safelyStoredLabel: 'Segmentos de Audio Guardados Seguros',
        transcribedLabel: 'Segmentos Transcritos',
        memoryProcessedLabel: 'Segmentos Procesados en Memoria',
        needsAttentionLabel: 'Elementos Marcados para Revisión del DM',
        continueProcessingBtn: '⚙ Continuar Procesamiento en Segundo Plano Ahora',
        finishLaterBtn: '🌙 Pausar Procesamiento y Terminar Más Tarde',
        finishLaterNote:
          'Al elegir "Terminar Más Tarde", todos los segmentos de audio sellados quedan en cola segura para que puedas cerrar Palantyr de inmediato.',
      },
      crashRecovery: {
        title: 'Sesión Interrumpida Detectada — Recuperación Autoritativa WAL .pseg',
        subtitle:
          'Palantyr detectó una sesión no cerrada tras un corte de energía o cierre inesperado. El registro Write-Ahead Log (`session_recording.pseg`) fue reconciliado con los archivos `.wav` en disco (FR-REC-006..008).',
        interruptedAtLabel: 'Marca de Tiempo de Interrupción',
        previouslySafeLabel: 'Segmentos Sellados Previamente',
        recoveredFromWalLabel: 'Segmentos Reconciliados y Recuperados del WAL',
        unrecoverableLabel: 'Segmentos Corruptos / Irrecuperables',
        recoveredDurationLabel: 'Audio Total de Sesión Preservado',
        walTableTitle: 'Registro de Integridad WAL por Segmento',
        colSeq: 'Sec #',
        colSegment: 'Archivo de Segmento',
        colDuration: 'Duración',
        colStatus: 'Estado WAL',
        choiceContinueTitle: '1. Continuar la Sesión Interrumpida',
        choiceContinueDesc: 'Reabre la sesión recuperada e inicia un nuevo segmento de audio secuencial.',
        choiceEndAndProcessTitle: '2. Finalizar Sesión y Procesar Audio Recuperado',
        choiceEndAndProcessDesc:
          'Marca la sesión recuperada como finalizada y pone sus segmentos en cola para transcripción y memoria.',
        choiceCloseAndStartNewTitle: '3. Cerrar de Forma Segura e Iniciar Nueva Sesión',
        choiceCloseAndStartNewDesc:
          'Conserva los segmentos recuperados en el archivo para después y vuelve al Hogar de Campaña en estado Inactivo.',
      },
      safeDelete: {
        title: 'Protocolo de Eliminación Segura de Campaña',
        subtitle:
          'Eliminar una campaña borra su base de datos SQLite aislada y su carpeta de audio. Palantyr recomienda encarecidamente crear primero un archivo portátil `.palantyr-backup` (AC-DATA-006).',
        warningText:
          'Selecciona cómo deseas proceder. "Crear Respaldo y Eliminar" exporta una copia verificada antes de borrar la campaña.',
        confirmNameLabel: 'Campaña seleccionada para eliminar:',
        backupAndDeleteBtn: '🛡 Crear Archivo de Respaldo y Eliminar Campaña (Recomendado)',
        deleteWithoutBackupBtn: '⚠ Eliminar Permanentemente Sin Respaldo',
        cancelBtn: 'Cancelar — Mantener Campaña Segura',
      },
      windowCloseGuard: {
        title: '¡Grabación de Sesión Activa en Curso!',
        subtitle:
          'Guardia de Protección de Cierre de Ventana activada (`NFR-REL-009`, `AC-REL-004`). Se desaconseja cerrar la aplicación abruptamente durante una grabación.',
        warningBody:
          'Palantyr está capturando audio de la mesa en este instante. ¿Deseas sellar de forma segura el segmento actual y finalizar la sesión antes de salir, o volver a tu sesión activa?',
        returnToSessionBtn: '← Volver a la Sesión Activa',
        endSessionAndExitBtn: '■ Finalizar Sesión de Forma Segura y Salir',
      },
      settings: {
        title: 'Ajustes de Palantyr y Custodia Local',
        subtitle: 'Configura el idioma bilingüe, modo de IA, accesibilidad y retención de archivos de audio.',
        uiLanguageLabel: 'Idioma de la Interfaz y Prompts (EN / ES)',
        processingModeLabel: 'Modo Activo de Procesamiento de IA',
        pauseBehaviorLabel: 'Al Pausar la Grabación de Sesión (FR-PROC-003)',
        microphoneDeviceLabel: 'Dispositivo de Entrada de Audio Predeterminado',
        testMicBtn: 'Probar Señal',
        accessibilitySection: 'Confort Visual y Accesibilidad (NFR-USAB-009)',
        reduceAnimationsLabel: 'Reducir Animaciones de Eonix y de la Interfaz',
        reduceAnimationsDesc:
          'Reemplaza el movimiento continuo del dragón, anillos pulsantes y transiciones por indicadores estáticos serenos (AC-USAB-004).',
        storageAndRetentionSection: 'Política de Retención de Audio y Espacio en Disco (NFR-DATA-006)',
        keepOriginalAudioLabel: 'Conservar Audio Original de Sesión (.wav) Tras Procesamiento Completo',
        keepOriginalAudioWarningTitle: 'ADVERTENCIA OBLIGATORIA DE ESPACIO EN DISCO (NFR-DATA-006 / AC-DATA-004):',
        keepOriginalAudioWarningBody:
          'Por defecto, Palantyr elimina los segmentos .wav originales solo después de que la transcripción Y la extracción de memoria estructurada hayan finalizado con éxito. Activar "Conservar Audio Original de Sesión" preserva indefinidamente los archivos PCM .wav sin comprimir (~320 MB a 650 MB por sesión de 4 horas), lo que puede consumir rápidamente gigabytes de almacenamiento SSD local.',
        acknowledgeAudioWarningCheckbox:
          'Comprendo el impacto en el espacio en disco local y deseo conservar indefinidamente los archivos de audio .wav originales.',
        rerunAssessmentBtn: '⟳ Volver a Ejecutar Evaluación de Hardware',
        saveSettingsBtn: 'Guardar Ajustes',
        closeBtn: 'Cerrar',
      },
    },
  },
};
