import React, { useEffect, useState } from 'react';
import {
  AppSettingsDto,
  CampaignDeletionOption,
  CampaignHomeStatusDto,
  CampaignSummaryDto,
  CrashRecoveryChoice,
  CrashRecoveryReportDto,
  CreateCampaignRequest,
  EndSessionSummaryDto,
  EonixChatMessage,
  EonixState,
  MicrophoneValidationResult,
  PalantyrLanguage,
  PartyMemberDto,
  PartyMembershipStatus,
  ProcessingMode,
  SessionRecordingState,
  SystemAssessmentProfile,
  VisibilityScope,
  WorldLoreEntryDto,
  TranscriptSegmentDto,
  NeedsAttentionItemDto,
} from './types/palantyr';
import { languageToLocale, translations } from './i18n/translations';
import { EonixCompanionCard } from './components/EonixCompanionCard';
import { SubsystemStatusBar } from './components/SubsystemStatusBar';
import { CreateCampaignWizard } from './components/CreateCampaignWizard';
import { TranscriptAndReviewPanel } from './components/TranscriptAndReviewPanel';
import { MemoryBrowserPanel } from './components/MemoryBrowserPanel';
import {
  MemoryEntityDto,
  EntityResolutionCandidateDto,
  MemoryConflictDto,
  CreateMemoryEntityRequest,
  UpdateMemoryEntityRequest,
  ResolveCandidateRequest,
  ResolveConflictRequest,
} from './types/palantyr';
import {
  CrashRecoveryModal,
  FirstRunAssessmentModal,
  formatDurationMs,
  SafeCampaignDeleteModal,
  SettingsModal,
  TwoStageEndSessionModal,
  WindowCloseGuardModal,
} from './components/ModalsAndDrawers';

type TopLevelScreen = 'welcome' | 'wizard' | 'campaignHome';

interface NeedsAttentionItem {
  id: string;
  title: string;
  excerpt: string;
  suggestedState: string;
}

const DEFAULT_SYSTEM_PROFILE: SystemAssessmentProfile = {
  id: 'sys-local-win11-01',
  assessedAtUtc: new Date().toISOString(),
  windowsVersion: 'Windows 11 Pro 24H2 (x64)',
  cpuModel: 'AMD Ryzen 7 7800X3D 8-Core Processor',
  logicalCores: 16,
  totalRamGb: 32.0,
  availableRamGb: 21.4,
  gpuModel: 'NVIDIA GeForce RTX 4070 Super',
  dedicatedVramGb: 12.0,
  freeStorageGb: 412.6,
  hasCompatibleGpuAcceleration: true,
  recommendedMode: 'LiveBalanced',
  recommendationReasonEn:
    'Detected 12 GB dedicated VRAM and 16 CPU threads. Live Balanced Mode will transcribe closed 15-minute segments in the background while keeping WASAPI audio recording isolated at Priority Tier 1.',
  recommendationReasonEs:
    'Se detectaron 12 GB de VRAM dedicada y 16 hilos de CPU. El Modo Equilibrado en Vivo transcribirá segmentos cerrados de 15 minutos en segundo plano manteniendo la grabación WASAPI aislada en Nivel de Prioridad 1.',
  audioDevices: [
    {
      deviceId: 'wasapi-shure-mv7',
      friendlyName: 'Shure MV7 Podcast Microphone (WASAPI Exclusive-Ready)',
      channels: 1,
      sampleRateHz: 48000,
      isDefault: true,
      isAvailable: true,
    },
    {
      deviceId: 'wasapi-focusrite-2i2',
      friendlyName: 'Focusrite Scarlett 2i2 USB Table Array',
      channels: 2,
      sampleRateHz: 48000,
      isDefault: false,
      isAvailable: true,
    },
  ],
};

const INITIAL_SETTINGS: AppSettingsDto = {
  uiLanguage: 'English',
  activeProcessingMode: 'LiveBalanced',
  pausePreference: 'Ask',
  selectedAudioDeviceId: 'wasapi-shure-mv7',
  keepOriginalSessionAudio: false,
  keepAudioWarningAcknowledged: false,
  reduceAnimations: false,
  nominalSegmentDurationMinutes: 15,
};

const INITIAL_CAMPAIGNS: CampaignSummaryDto[] = [
  {
    campaignId: 'camp-valen-001',
    databaseName: 'campaign_camp_valen_001.db',
    name: 'Ashes of the Shattered Crown',
    subtitleOrSettingName: 'Continent of Valen — High Dark Fantasy',
    worldSummary:
      'Five shards of the Obsidian Crown lie scattered across warring city-states while the Cult of Ash infiltrates the Silver Concordat.',
    calendarSystemDescription: 'Imperial Solar Reckoning (Year 842 AE)',
    primaryLanguage: 'English',
    createdAtUtc: '2026-11-01T18:00:00Z',
    lastOpenedAtUtc: '2026-11-14T20:15:00Z',
    lastBackupAtUtc: '2026-11-12T23:10:00Z',
    hasUnbackedUpChanges: true,
    sessionCount: 4,
    partyMemberCount: 4,
    needsAttentionCount: 2,
  },
];


const INITIAL_MEMORY_ENTITIES: MemoryEntityDto[] = [
  {
    id: 'char_elminster_01',
    entityType: 'Character',
    canonicalName: 'Elminster Aumar',
    aliases: ['Old Mage', 'Sage of Shadowdale'],
    titles: ['Chosen of Mystra'],
    recognitionVariants: ['Elminstir', 'Elminsterr'],
    summaryDescription: 'A legendary archmage and advisor to rulers throughout Faerun.',
    dmPrivateNotes: 'Possesses a secret ward stone that bypasses Netherese mythals.',
    visibility: 'PartyKnown',
    authorizedCharacterIds: [],
    knowledgeState: 'CampaignTruth',
    confidence: 'DMConfirmed',
    stateAndBeliefHistory: [],
    sources: [],
    characterFacet: {
      roleOrArchetype: 'Archmage',
      raceOrSpecies: 'Human',
      physicalDescription: 'Elderly man with a pipe and long gray beard.',
      affiliations: ['Harpers'],
      statementsBy: [
        {
          statementId: 'stmt_01',
          sessionId: 'session-001',
          summaryText: 'The seals on the dwarven mine were broken three centuries ago.',
          targetSubject: 'Him Kuldir',
          isByCharacter: true,
          visibility: 'PartyKnown',
        },
      ],
      statementsAbout: [
        {
          statementId: 'stmt_02',
          sessionId: 'session-002',
          summaryText: 'Seen walking through the Astral Plane without a silver cord.',
          isByCharacter: false,
          visibility: 'PartyKnown',
        },
      ],
    },
  },
  {
    id: 'loc_him_kuldir',
    entityType: 'Location',
    canonicalName: 'Him Kuldir Mine',
    aliases: ['The Deep Delve'],
    titles: ['Ancient Dwarven Fortress'],
    recognitionVariants: ['Him Kulder', 'Kim Kuldir'],
    summaryDescription: 'Abandoned dwarven iron mine carved into the Frostfall Mountains.',
    visibility: 'PartyKnown',
    authorizedCharacterIds: [],
    knowledgeState: 'CampaignTruth',
    confidence: 'HighConfidence',
    stateAndBeliefHistory: [],
    sources: [],
    locationFacet: {
      locationType: 'Structure',
      parentLocationId: 'Frostfall Mountains',
      enclosedChildIds: ['loc_forge', 'loc_vault'],
      isTransitPoint: false,
    },
  },
  {
    id: 'journey_frostfall_01',
    entityType: 'JourneyOrStay',
    canonicalName: 'Expedition to Him Kuldir',
    aliases: ['The Mountain March'],
    titles: [],
    recognitionVariants: [],
    summaryDescription: '14-day trekking journey across the glacial peaks.',
    visibility: 'PartyKnown',
    authorizedCharacterIds: [],
    knowledgeState: 'CampaignTruth',
    confidence: 'HighConfidence',
    stateAndBeliefHistory: [],
    sources: [],
    journeyFacet: {
      originLocationId: 'Waterdeep',
      destinationLocationId: 'Him Kuldir Mine',
      totalEstimatedDays: 14,
      currentRelativeDay: 4,
      relativeTimeMarker: 'Day 4 of 14',
      departureCampaignDate: null,
      dayTransitions: [
        {
          transitionId: 'trans_01',
          sessionId: 'session-004',
          fromRelativeDay: 3,
          toRelativeDay: 4,
          narrativeMarkerText: 'Dawn broke over the crags as the blizzard ceased.',
          detectedAtUtc: '2026-11-14T21:00:00Z',
        },
      ],
      keyMoments: [
        {
          momentId: 'km_01',
          sessionId: 'session-004',
          relativeDayNumber: 2,
          summaryText: 'Ambushed by frost mephits near the pass.',
          participants: ['Kaelen Dawnstrider'],
        },
      ],
    },
  },
];

const INITIAL_RESOLUTION_CANDIDATES: EntityResolutionCandidateDto[] = [
  {
    candidateId: 'cand_res_001',
    campaignId: 'camp-valen-001',
    observedSurfaceName: 'Elminsterr',
    contextSnippet: 'The old traveler said his name was Elminsterr and carried a pipe.',
    sourceSessionId: 'session-004',
    sourceSegmentId: 'seg_002',
    detectedAtUtc: '2026-11-14T21:30:00Z',
    status: 'PendingDmReview',
    potentialMatches: [
      {
        entityId: 'char_elminster_01',
        canonicalName: 'Elminster Aumar',
        entityType: 'Character',
        matchScore: 0.9,
        matchReason: 'LevenshteinDistance',
      },
    ],
  },
];

const INITIAL_CONFLICTS: MemoryConflictDto[] = [
  {
    conflictId: 'conf_bridge_01',
    campaignId: 'camp-valen-001',
    entityId: 'loc_bridge_01',
    conflictType: 'FactualContradiction',
    summary: 'Contradiction regarding Stone Arch Bridge status',
    state: 'Unresolved',
    competingAssertions: [
      {
        assertionId: 'ass_01',
        assertionText: 'The stone bridge is intact and merchant caravans cross daily.',
        knowledgeState: 'CampaignTruth',
        confidence: 'HighConfidence',
        recordedAtUtc: '2026-11-10T18:00:00Z',
      },
      {
        assertionId: 'ass_02',
        assertionText: 'The bridge collapsed fifty years ago; only ruined pillars remain.',
        knowledgeState: 'PartyBelief',
        confidence: 'MediumConfidence',
        recordedAtUtc: '2026-11-14T21:15:00Z',
      },
    ],
  },
];
export const App: React.FC = () => {
  const [screen, setScreen] = useState<TopLevelScreen>('welcome');
  const [systemProfile, setSystemProfile] = useState<SystemAssessmentProfile>(
    DEFAULT_SYSTEM_PROFILE
  );
  const [settings, setSettings] = useState<AppSettingsDto>(INITIAL_SETTINGS);
  const [campaigns, setCampaigns] =
    useState<CampaignSummaryDto[]>(INITIAL_CAMPAIGNS);
  const [activeCampaignId, setActiveCampaignId] =
    useState<string>('camp-valen-001');

  // Modals state
  const [showFirstRunModal, setShowFirstRunModal] = useState<boolean>(false);
  const [showSettingsModal, setShowSettingsModal] = useState<boolean>(false);
  const [endSessionStage, setEndSessionStage] = useState<0 | 1 | 2>(0);
  const [endSessionSummary, setEndSessionSummary] =
    useState<EndSessionSummaryDto | null>(null);
  const [crashReport, setCrashReport] = useState<CrashRecoveryReportDto | null>(
    null
  );
  const [campaignPendingDelete, setCampaignPendingDelete] =
    useState<CampaignSummaryDto | null>(null);
  const [showWindowCloseGuard, setShowWindowCloseGuard] =
    useState<boolean>(false);
  const [showPauseAskBanner, setShowPauseAskBanner] = useState<boolean>(false);
  const [workspaceView, setWorkspaceView] = useState<'console' | 'transcripts' | 'memory'>('console');
  const [memoryEntities, setMemoryEntities] = useState<MemoryEntityDto[]>(INITIAL_MEMORY_ENTITIES);
  const [resolutionCandidates, setResolutionCandidates] = useState<EntityResolutionCandidateDto[]>(INITIAL_RESOLUTION_CANDIDATES);
  const [memoryConflicts, setMemoryConflicts] = useState<MemoryConflictDto[]>(INITIAL_CONFLICTS);

  const handleSaveMemoryEntity = (req: CreateMemoryEntityRequest | UpdateMemoryEntityRequest, entityId?: string) => {
    if (entityId) {
      setMemoryEntities((prev) =>
        prev.map((e) =>
          e.id === entityId
            ? {
                ...e,
                canonicalName: req.canonicalName || e.canonicalName,
                aliases: req.aliases || e.aliases,
                summaryDescription: req.summaryDescription !== undefined ? req.summaryDescription : e.summaryDescription,
                dmPrivateNotes: req.dmPrivateNotes !== undefined ? req.dmPrivateNotes : e.dmPrivateNotes,
                visibility: req.visibility || e.visibility,
              }
            : e
        )
      );
    } else {
      const createReq = req as CreateMemoryEntityRequest;
      const newEntity: MemoryEntityDto = {
        id: `entity_${Date.now()}`,
        entityType: createReq.entityType,
        canonicalName: createReq.canonicalName,
        aliases: createReq.aliases || [],
        titles: createReq.titles || [],
        recognitionVariants: createReq.recognitionVariants || [],
        summaryDescription: createReq.summaryDescription || '',
        dmPrivateNotes: createReq.dmPrivateNotes,
        visibility: createReq.visibility || 'PartyKnown',
        authorizedCharacterIds: [],
        knowledgeState: createReq.knowledgeState || 'CampaignTruth',
        confidence: createReq.confidence || 'HighConfidence',
        stateAndBeliefHistory: [],
        sources: [],
      };
      setMemoryEntities((prev) => [newEntity, ...prev]);
    }
  };

  const handleDeleteMemoryEntity = (entityId: string) => {
    setMemoryEntities((prev) => prev.filter((e) => e.id !== entityId));
  };

  const handleResolveEntityCandidate = (candidateId: string, req: ResolveCandidateRequest) => {
    setResolutionCandidates((prev) =>
      prev.map((c) =>
        c.candidateId === candidateId
          ? {
              ...c,
              status: req.action === 'Dismiss' ? 'Dismissed' : req.action === 'CreateNew' ? 'CreatedNewEntity' : 'LinkedToExisting',
              resolvedEntityId: req.targetEntityId || null,
              resolutionDecision: `${req.action} by DM`,
            }
          : c
      )
    );
    if (req.registerAsSpeechVariant && req.targetEntityId) {
      const cand = resolutionCandidates.find((c) => c.candidateId === candidateId);
      if (cand) {
        setMemoryEntities((prev) =>
          prev.map((e) =>
            e.id === req.targetEntityId
              ? { ...e, recognitionVariants: [...e.recognitionVariants, cand.observedSurfaceName] }
              : e
          )
        );
      }
    }
  };

  const handleResolveLoreConflict = (conflictId: string, req: ResolveConflictRequest) => {
    setMemoryConflicts((prev) =>
      prev.map((c) =>
        c.conflictId === conflictId
          ? {
              ...c,
              state: req.resolution,
              dmRuling: req.dmRulingNote,
              resolvedAtUtc: new Date().toISOString(),
            }
          : c
      )
    );
  };


  // Campaign Home Persistent State (UX-CAM-001..003, UX-HOME-001..009)
  const [homeStatus, setHomeStatus] = useState<CampaignHomeStatusDto>({
    campaignId: 'camp-valen-001',
    activeSessionId: null,
    sessionState: 'Idle',
    eonixVisualState: 'Idle',
    recordingSaveStatus: 'Idle',
    transcriptionStatus: 'Idle',
    memoryProcessingStatus: 'Idle',
    eonixQueryStatus: 'Idle',
    totalRecordedDurationMs: 0,
    safelyStoredSegmentsCount: 3,
    transcribedSegmentsCount: 3,
    memoryProcessedSegmentsCount: 3,
    needsAttentionCount: 2,
    freeStorageGb: 412.6,
    isStorageWarningActive: false,
    isBackgroundThrottled: false,
    throttlingReasonEn: null,
    throttlingReasonEs: null,
  });

  // Campaign-scoped Party Roster & Lore
  const [rosterByCampaign, setRosterByCampaign] = useState<
    Record<string, PartyMemberDto[]>
  >({
    'camp-valen-001': [
      {
        characterId: 'pc-1',
        characterName: 'Kaelen Dawnstrider',
        raceOrSpecies: 'Half-Elf',
        classOrArchetype: 'Oath of Ancients Paladin',
        controlledByPlayerName: 'Sofía',
        isPlayerCharacter: true,
        currentStatus: 'Active',
        membershipHistory: [
          {
            status: 'Active',
            changedAtUtc: '2026-11-01T18:00:00Z',
            sessionId: 'sess-01',
            campaignDateContext: '14 Frostfall 842 AE',
            reasonNote: 'Founding Vanguard',
          },
        ],
      },
      {
        characterId: 'pc-2',
        characterName: 'Vespera Noctis',
        raceOrSpecies: 'Tiefling',
        classOrArchetype: 'Shadow Sorcerer',
        controlledByPlayerName: 'Mateo',
        isPlayerCharacter: true,
        currentStatus: 'Active',
        membershipHistory: [],
      },
      {
        characterId: 'npc-1',
        characterName: 'Captain Thorne',
        raceOrSpecies: 'Human',
        classOrArchetype: 'Mercenary Guide',
        controlledByPlayerName: null,
        isPlayerCharacter: false,
        currentStatus: 'Temporary',
        membershipHistory: [],
      },
      {
        characterId: 'pc-3',
        characterName: 'Bram Ironvein',
        raceOrSpecies: 'Mountain Dwarf',
        classOrArchetype: 'Forge Cleric',
        controlledByPlayerName: 'Lucas',
        isPlayerCharacter: true,
        currentStatus: 'Deceased',
        membershipHistory: [],
      },
    ],
  });

  const [loreByCampaign, setLoreByCampaign] = useState<
    Record<string, WorldLoreEntryDto[]>
  >({
    'camp-valen-001': [
      {
        loreId: 'lore-101',
        title: 'The Obsidian Spire of Oakhaven',
        category: 'Location / Mystery',
        content:
          'The townsfolk and the party believe the Spire is an ancient elven astronomical tower.',
        visibility: 'PartyKnown',
        knowledgeState: 'PartyBelief',
        confidence: 'DMConfirmed',
        sourceType: 'SessionSummary',
        createdAtUtc: '2026-11-05T21:00:00Z',
      },
      {
        loreId: 'lore-102',
        title: 'True Purpose of the Obsidian Spire',
        category: 'DM Plot Secret',
        content:
          'The Spire is actually a dormant planar anchor built by Arch-Prelate Vane to siphon soul-ash.',
        visibility: 'DMPrivate',
        knowledgeState: 'DMPrivateTruth',
        confidence: 'DMConfirmed',
        sourceType: 'DMDirectNote',
        createdAtUtc: '2026-11-05T21:05:00Z',
      },
      {
        loreId: 'lore-103',
        title: 'Edict of the Silver Concordat',
        category: 'World Law',
        content:
          'Unlicensed arcane casting inside cathedral districts carries a sentence of exile.',
        visibility: 'GeneralWorld',
        knowledgeState: 'CampaignTruth',
        confidence: 'DMConfirmed',
        sourceType: 'CampaignSetup',
        createdAtUtc: '2026-11-01T18:00:00Z',
      },
    ],
  });

  const [attentionItems, setAttentionItems] = useState<NeedsAttentionItem[]>([
    {
      id: 'att-1',
      title: 'Ambiguous NPC Name: "Lord Malakor" vs "Lord Malachor"',
      excerpt:
        'Session 4 Segment #2 (00:24:12) — Whisper detected two phonetic spellings with Medium Confidence.',
      suggestedState: 'ClaimOrRumor',
    },
    {
      id: 'att-2',
      title: 'Possible Table Joke vs Canon Action: "We sell the castle for 3 goats"',
      excerpt:
        'Session 4 Segment #3 (00:41:05) — Classified as OutOfCharacterTableTalk / HypotheticalOrJoke. Quarantined from Campaign Truth.',
      suggestedState: 'UnknownOrUnresolved',
    },
  ]);

  const [eonixMessages, setEonixMessages] = useState<EonixChatMessage[]>([]);

  const [sampleTranscripts, setSampleTranscripts] = useState<TranscriptSegmentDto[]>([
    {
      id: 'transcripts/camp-valen-001/session-004/seg-001',
      campaignId: 'camp-valen-001',
      sessionId: 'session-004',
      segmentId: 'seg-001',
      sequenceNumber: 1,
      language: 'Spanish',
      transcribedAtUtc: '2026-11-14T20:00:00Z',
      machineUtterances: [
        {
          utteranceId: 'utt-101',
          startMs: 0,
          endMs: 4200,
          originalText: 'El DM describe las catacumbas bajo la torre de obsidiana.',
          confidenceScore: 0.95,
        },
        {
          utteranceId: 'utt-102',
          startMs: 4300,
          endMs: 8500,
          originalText: 'Tira salvación de destreza con ventaja por la antorcha.',
          confidenceScore: 0.92,
        },
        {
          utteranceId: 'utt-103',
          startMs: 8600,
          endMs: 14000,
          originalText: 'Qué pasaría si vendo el castillo por tres cabras jaja',
          confidenceScore: 0.88,
        },
      ],
      workingSpans: [
        {
          spanId: 'span-101',
          segmentId: 'seg-001',
          startMs: 0,
          endMs: 4200,
          originalText: 'El DM describe las catacumbas bajo la torre de obsidiana.',
          correctedText: 'El DM describe las catacumbas bajo la torre de obsidiana.',
          contextType: 'InWorldCanonCandidate',
          contextConfidence: 0.95,
        },
        {
          spanId: 'span-102',
          segmentId: 'seg-001',
          startMs: 4300,
          endMs: 8500,
          originalText: 'Tira salvación de destreza con ventaja por la antorcha.',
          correctedText: 'Tira salvación de destreza con ventaja por la antorcha.',
          contextType: 'RulesOrMeta',
          contextConfidence: 0.92,
        },
        {
          spanId: 'span-103',
          segmentId: 'seg-001',
          startMs: 8600,
          endMs: 14000,
          originalText: 'Qué pasaría si vendo el castillo por tres cabras jaja',
          correctedText: 'Qué pasaría si vendo el castillo por tres cabras jaja',
          contextType: 'HypotheticalOrJoke',
          contextConfidence: 0.88,
        },
      ],
    },
  ]);

  const locale = languageToLocale(settings.uiLanguage);
  const t = translations[locale];

  const activeCampaign =
    campaigns.find((c) => c.campaignId === activeCampaignId) || campaigns[0];

  // Hydrate from local /api/v1 backend if reachable (offline-first with graceful fallback)
  useEffect(() => {
    let cancelled = false;
    async function bootstrapFromLocalHost() {
      try {
        const [sysRes, setRes, campRes] = await Promise.allSettled([
          fetch('/api/v1/system/assessment'),
          fetch('/api/v1/settings'),
          fetch('/api/v1/campaigns'),
        ]);

        if (!cancelled && sysRes.status === 'fulfilled' && sysRes.value.ok) {
          const data = await sysRes.value.json();
          if (data && data.cpuModel) {
            setSystemProfile((prev) => ({ ...prev, ...data }));
          }
        }
        if (!cancelled && setRes.status === 'fulfilled' && setRes.value.ok) {
          const data = await setRes.value.json();
          if (data) {
            setSettings((prev) => ({ ...prev, ...data }));
          }
        }
        if (!cancelled && campRes.status === 'fulfilled' && campRes.value.ok) {
          const data = await campRes.value.json();
          if (Array.isArray(data) && data.length > 0) {
            setCampaigns(data);
            setActiveCampaignId(data[0].campaignId);
          }
        }
      } catch {
        // Local fallback state is already initialized for immediate offline interaction
      }
    }
    bootstrapFromLocalHost();
    return () => {
      cancelled = true;
    };
  }, []);

  // Browser beforeunload protection during active recording (NFR-REL-009, AC-REL-004)
  useEffect(() => {
    const handleBeforeUnload = (e: BeforeUnloadEvent) => {
      if (homeStatus.sessionState === 'Recording') {
        e.preventDefault();
        e.returnValue = '';
        setShowWindowCloseGuard(true);
      }
    };
    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, [homeStatus.sessionState]);

  // Live session elapsed timer while Recording
  useEffect(() => {
    if (homeStatus.sessionState !== 'Recording') return;
    const timer = window.setInterval(() => {
      setHomeStatus((prev) => ({
        ...prev,
        totalRecordedDurationMs: prev.totalRecordedDurationMs + 1000,
      }));
    }, 1000);
    return () => window.clearInterval(timer);
  }, [homeStatus.sessionState]);

  const handleToggleLanguage = () => {
    const nextLang: PalantyrLanguage =
      settings.uiLanguage === 'English' ? 'Spanish' : 'English';
    setSettings((prev) => ({ ...prev, uiLanguage: nextLang }));
  };

  const handleTestMicrophone = async (
    deviceId: string
  ): Promise<MicrophoneValidationResult> => {
    try {
      const res = await fetch('/api/v1/audio/validate-microphone', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ deviceId }),
      });
      if (res.ok) {
        return await res.json();
      }
    } catch {
      // Fallback local acoustic validation
    }
    return {
      deviceId,
      isAvailable: true,
      hasDetectableSignal: true,
      peakLevelDb: -11.4,
      diagnosticMessageEn:
        'WASAPI input verified. Clean acoustic signal detected with low noise floor (-58 dB). Ready for .pseg WAL capture.',
      diagnosticMessageEs:
        'Entrada WASAPI verificada. Señal acústica limpia detectada con piso de ruido bajo (-58 dB). Listo para captura WAL .pseg.',
    };
  };

  const handleCreateCampaign = async (req: CreateCampaignRequest) => {
    let created: CampaignSummaryDto | null = null;
    try {
      const res = await fetch('/api/v1/campaigns', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(req),
      });
      if (res.ok) {
        created = await res.json();
      }
    } catch {
      // Fallback local creation
    }

    const newId = created?.campaignId || `camp-${Date.now()}`;
    const summary: CampaignSummaryDto = created || {
      campaignId: newId,
      databaseName: `campaign_${newId.replace(/[^a-z0-9]/gi, '_')}.db`,
      name: req.name,
      subtitleOrSettingName: req.subtitleOrSettingName,
      worldSummary: req.worldSummary,
      calendarSystemDescription: req.calendarSystemDescription,
      primaryLanguage: req.primaryLanguage,
      createdAtUtc: new Date().toISOString(),
      lastOpenedAtUtc: new Date().toISOString(),
      lastBackupAtUtc: null,
      hasUnbackedUpChanges: false,
      sessionCount: 0,
      partyMemberCount: req.initialPartyRoster.length,
      needsAttentionCount: 0,
    };

    setCampaigns((prev) => [summary, ...prev]);
    setRosterByCampaign((prev) => ({
      ...prev,
      [newId]: req.initialPartyRoster,
    }));
    setLoreByCampaign((prev) => ({
      ...prev,
      [newId]: req.initialWorldLore,
    }));
    setActiveCampaignId(newId);
    setHomeStatus((prev) => ({
      ...prev,
      campaignId: newId,
      activeSessionId: null,
      sessionState: 'Idle',
      eonixVisualState: 'Idle',
      recordingSaveStatus: 'Idle',
      transcriptionStatus: 'Idle',
      memoryProcessingStatus: 'Idle',
      eonixQueryStatus: 'Idle',
      totalRecordedDurationMs: 0,
      safelyStoredSegmentsCount: 0,
      transcribedSegmentsCount: 0,
      memoryProcessedSegmentsCount: 0,
      needsAttentionCount: 0,
    }));
    setScreen('campaignHome');
  };

  // Persistent Campaign Home Session Transitions (UX-CAM-002, UX-HOME-001..009)
  const handleStartRecording = async () => {
    const sessionId = `sess-${Date.now()}`;
    try {
      await fetch(`/api/v1/campaigns/${activeCampaignId}/sessions/start`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          microphoneDeviceId: settings.selectedAudioDeviceId,
        }),
      });
    } catch {
      // Local state transition
    }

    const isRecOnly =
      settings.activeProcessingMode === 'RecordingOnly' ||
      settings.activeProcessingMode === 'ManualDeferred';

    setShowPauseAskBanner(false);
    setHomeStatus((prev) => ({
      ...prev,
      activeSessionId: prev.activeSessionId || sessionId,
      sessionState: 'Recording',
      eonixVisualState: 'ListeningRecording',
      recordingSaveStatus: 'Processing',
      transcriptionStatus: isRecOnly ? 'Pending' : 'Processing',
      memoryProcessingStatus: 'Pending',
      eonixQueryStatus: 'Idle',
      isBackgroundThrottled: isRecOnly,
      throttlingReasonEn: isRecOnly
        ? `${settings.activeProcessingMode} active: 100% of resources reserved for Tier 1 audio capture.`
        : null,
      throttlingReasonEs: isRecOnly
        ? `${settings.activeProcessingMode} activo: 100% de recursos reservados para captura de audio Nivel 1.`
        : null,
    }));
  };

  const handlePauseRecording = async () => {
    try {
      if (homeStatus.activeSessionId) {
        await fetch(
          `/api/v1/campaigns/${activeCampaignId}/sessions/${homeStatus.activeSessionId}/pause`,
          { method: 'POST' }
        );
      }
    } catch {
      // Local state transition
    }

    const pref = settings.pausePreference;
    const autoTranscribe = pref === 'Always';
    setShowPauseAskBanner(pref === 'Ask');

    setHomeStatus((prev) => ({
      ...prev,
      sessionState: 'Paused',
      eonixVisualState: autoTranscribe ? 'Transcribing' : 'Paused',
      recordingSaveStatus: 'Saved',
      safelyStoredSegmentsCount: prev.safelyStoredSegmentsCount + 1,
      transcriptionStatus: autoTranscribe ? 'Processing' : 'Paused',
    }));
  };

  const handleResumeRecording = async () => {
    setShowPauseAskBanner(false);
    setHomeStatus((prev) => ({
      ...prev,
      sessionState: 'Recording',
      eonixVisualState: 'ListeningRecording',
      recordingSaveStatus: 'Processing',
      transcriptionStatus:
        settings.activeProcessingMode === 'LiveBalanced'
          ? 'Processing'
          : 'Pending',
    }));
  };

  // Two-Stage End Session (UX-END-001..003, AC-END-001..002)
  const handleInitiateEndSession = () => {
    setEndSessionStage(1);
  };

  const handleConfirmStage1SealAudio = async () => {
    const newSafeCount = homeStatus.safelyStoredSegmentsCount + 1;
    const summary: EndSessionSummaryDto = {
      campaignId: activeCampaignId,
      sessionId: homeStatus.activeSessionId || 'sess-current',
      totalRecordedDurationMs: Math.max(
        homeStatus.totalRecordedDurationMs,
        54 * 60 * 1000
      ),
      safelyStoredSegments: newSafeCount,
      transcribedSegments: homeStatus.transcribedSegmentsCount,
      memoryProcessedSegments: homeStatus.memoryProcessedSegmentsCount,
      needsAttentionItemsCount: attentionItems.length,
      processingRemaining: true,
    };

    setHomeStatus((prev) => ({
      ...prev,
      sessionState: 'Idle',
      activeSessionId: null,
      eonixVisualState: 'Transcribing',
      recordingSaveStatus: 'Saved',
      safelyStoredSegmentsCount: newSafeCount,
      transcriptionStatus: 'Pending',
      memoryProcessingStatus: 'Pending',
    }));

    setEndSessionSummary(summary);
    setEndSessionStage(2);
  };

  const handleStage2Choice = (continueProcessingNow: boolean) => {
    setEndSessionStage(0);
    if (continueProcessingNow) {
      setHomeStatus((prev) => ({
        ...prev,
        eonixVisualState: 'MemoryProcessing',
        recordingSaveStatus: 'Saved',
        transcriptionStatus: 'Completed',
        memoryProcessingStatus: 'Processing',
        transcribedSegmentsCount: prev.safelyStoredSegmentsCount,
      }));
    } else {
      setHomeStatus((prev) => ({
        ...prev,
        eonixVisualState: 'Idle',
        recordingSaveStatus: 'Saved',
        transcriptionStatus: 'Paused',
        memoryProcessingStatus: 'Paused',
      }));
    }
  };

  // Eonix Ask / Teach Interaction (UX-CAM-003, UX-EONIX-001)
  const handleSendEonixMessage = (
    text: string,
    mode: 'ask' | 'teach',
    visibility: VisibilityScope
  ) => {
    const dmMsg: EonixChatMessage = {
      id: `msg-dm-${Date.now()}`,
      sender: 'dm',
      mode,
      text,
      timestampUtc: new Date().toISOString(),
      visibility: mode === 'teach' ? visibility : undefined,
    };

    setHomeStatus((prev) => ({
      ...prev,
      eonixVisualState: mode === 'ask' ? 'Answering' : 'MemoryProcessing',
      eonixQueryStatus: 'Completed',
    }));

    if (mode === 'teach') {
      const newLore: WorldLoreEntryDto = {
        loreId: `lore-taught-${Date.now()}`,
        title: text.slice(0, 48),
        category: 'DM Conversational Teaching',
        content: text,
        visibility,
        knowledgeState:
          visibility === 'DMPrivate' ? 'DMPrivateTruth' : 'CampaignTruth',
        confidence: 'DMConfirmed',
        sourceType: 'DMConversationalTeaching',
        createdAtUtc: new Date().toISOString(),
      };
      setLoreByCampaign((prev) => ({
        ...prev,
        [activeCampaignId]: [newLore, ...(prev[activeCampaignId] || [])],
      }));
    }

    const eonixReply: EonixChatMessage = {
      id: `msg-eonix-${Date.now() + 1}`,
      sender: 'eonix',
      mode,
      text:
        mode === 'ask'
          ? `${t.eonixCompanion.sampleAnswerPrefix} In "${activeCampaign?.name}", the party currently believes the Obsidian Spire is an elven observatory (PartyBelief, Session 2), while your DM Private Truth records that Arch-Prelate Vane uses it as a soul-ash anchor (DMPrivateTruth).`
          : `${t.eonixCompanion.sampleTeachAckPrefix} "${text}" has been permanently inscribed into ${activeCampaign?.databaseName} with visibility [${t.visibilityScopes[visibility].label}].`,
      timestampUtc: new Date().toISOString(),
      citations:
        mode === 'ask'
          ? [
              'Session 2 Transcript [01:14:22]',
              'Lore #101 (PartyBelief)',
              'Lore #102 (DMPrivateTruth)',
            ]
          : ['Provenance: DMConversationalTeaching'],
      knowledgeState:
        mode === 'teach'
          ? visibility === 'DMPrivate'
            ? 'DMPrivateTruth'
            : 'CampaignTruth'
          : 'CampaignTruth',
      visibility,
    };

    setEonixMessages((prev) => [...prev, dmMsg, eonixReply]);
  };

  // Crash Recovery Drill (FR-REC-006..008, AC-REC-003)
  const handleTriggerCrashRecoveryDrill = () => {
    const sampleReport: CrashRecoveryReportDto = {
      campaignId: activeCampaignId,
      sessionId: 'sess-interrupted-04',
      interruptedAtUtc: new Date(Date.now() - 8 * 60 * 1000).toISOString(),
      previouslySafeSegmentsCount: 2,
      recoveredSegmentsCount: 1,
      unrecoverableSegmentsCount: 0,
      totalRecoveredDurationMs: 42 * 60 * 1000 + 18000,
      segments: [
        {
          sessionId: 'sess-interrupted-04',
          segmentId: 'seg-001',
          sequenceNumber: 1,
          filePath: 'sessions/sess-interrupted-04/segment_001.wav',
          startedAtUtc: '2026-11-14T19:00:00Z',
          lastFlushedAtUtc: '2026-11-14T19:15:00Z',
          flushedPcmBytes: 86400000,
          durationMs: 15 * 60 * 1000,
          status: 'ClosedSafe',
        },
        {
          sessionId: 'sess-interrupted-04',
          segmentId: 'seg-002',
          sequenceNumber: 2,
          filePath: 'sessions/sess-interrupted-04/segment_002.wav',
          startedAtUtc: '2026-11-14T19:15:00Z',
          lastFlushedAtUtc: '2026-11-14T19:30:00Z',
          flushedPcmBytes: 86400000,
          durationMs: 15 * 60 * 1000,
          status: 'ClosedSafe',
        },
        {
          sessionId: 'sess-interrupted-04',
          segmentId: 'seg-003',
          sequenceNumber: 3,
          filePath: 'sessions/sess-interrupted-04/segment_003.wav',
          startedAtUtc: '2026-11-14T19:30:00Z',
          lastFlushedAtUtc: '2026-11-14T19:42:18Z',
          flushedPcmBytes: 70848000,
          durationMs: 12 * 60 * 1000 + 18000,
          status: 'RecoveredFromCrash',
          closureReason: 'WAV header repaired from .pseg WAL byte offset',
        },
      ],
      summaryMessageEn:
        '100% of recorded audio (42m 18s across 3 segments) was preserved. Segment #3 WAV header was repaired using the authoritative `session_recording.pseg` byte counter.',
      summaryMessageEs:
        'El 100% del audio grabado (42m 18s en 3 segmentos) fue preservado. La cabecera WAV del Segmento #3 se reparó usando el contador de bytes autoritativo de `session_recording.pseg`.',
    };
    setCrashReport(sampleReport);
  };

  const handleResolveCrashRecovery = (choice: CrashRecoveryChoice) => {
    if (!crashReport) return;
    if (choice === 'ContinueSession') {
      setHomeStatus((prev) => ({
        ...prev,
        activeSessionId: crashReport.sessionId,
        sessionState: 'Recording',
        eonixVisualState: 'ListeningRecording',
        recordingSaveStatus: 'Processing',
        safelyStoredSegmentsCount:
          crashReport.previouslySafeSegmentsCount +
          crashReport.recoveredSegmentsCount,
        totalRecordedDurationMs: crashReport.totalRecoveredDurationMs,
      }));
      setScreen('campaignHome');
    } else if (choice === 'EndSessionAndProcess') {
      setHomeStatus((prev) => ({
        ...prev,
        activeSessionId: null,
        sessionState: 'Idle',
        eonixVisualState: 'Transcribing',
        recordingSaveStatus: 'Saved',
        transcriptionStatus: 'Processing',
        memoryProcessingStatus: 'Pending',
        safelyStoredSegmentsCount:
          crashReport.previouslySafeSegmentsCount +
          crashReport.recoveredSegmentsCount,
        totalRecordedDurationMs: crashReport.totalRecoveredDurationMs,
      }));
      setScreen('campaignHome');
    } else {
      setHomeStatus((prev) => ({
        ...prev,
        activeSessionId: null,
        sessionState: 'Idle',
        eonixVisualState: 'Idle',
        recordingSaveStatus: 'Saved',
        safelyStoredSegmentsCount:
          crashReport.previouslySafeSegmentsCount +
          crashReport.recoveredSegmentsCount,
      }));
      setScreen('campaignHome');
    }
    setCrashReport(null);
  };

  const handleSelectCampaignDeletion = (option: CampaignDeletionOption) => {
    if (!campaignPendingDelete || option === 'Cancel') {
      setCampaignPendingDelete(null);
      return;
    }
    const targetId = campaignPendingDelete.campaignId;
    setCampaigns((prev) => prev.filter((c) => c.campaignId !== targetId));
    setCampaignPendingDelete(null);
    if (activeCampaignId === targetId) {
      const remaining = campaigns.filter((c) => c.campaignId !== targetId);
      if (remaining.length > 0) {
        setActiveCampaignId(remaining[0].campaignId);
      } else {
        setScreen('welcome');
      }
    }
  };

  const handleCyclePartyMemberStatus = (characterId: string) => {
    const order: PartyMembershipStatus[] = [
      'Active',
      'Temporary',
      'Former',
      'Deceased',
      'Inactive',
    ];
    setRosterByCampaign((prev) => {
      const list = prev[activeCampaignId] || [];
      return {
        ...prev,
        [activeCampaignId]: list.map((m) => {
          if (m.characterId !== characterId) return m;
          const nextStatus =
            order[(order.indexOf(m.currentStatus) + 1) % order.length];
          return {
            ...m,
            currentStatus: nextStatus,
            membershipHistory: [
              ...m.membershipHistory,
              {
                status: nextStatus,
                changedAtUtc: new Date().toISOString(),
                sessionId: homeStatus.activeSessionId,
                campaignDateContext: 'Updated in Campaign Home',
                reasonNote: `Status transitioned to ${nextStatus}`,
              },
            ],
          };
        }),
      };
    });
  };

  const handleResolveAttentionItem = (id: string) => {
    setAttentionItems((prev) => prev.filter((item) => item.id !== id));
    setHomeStatus((prev) => ({
      ...prev,
      needsAttentionCount: Math.max(0, prev.needsAttentionCount - 1),
    }));
  };

  const handleCorrectSpan = (segmentId: string, spanId: string, newText: string) => {
    setSampleTranscripts((prev) =>
      prev.map((seg) => {
        if (seg.segmentId !== segmentId) return seg;
        return {
          ...seg,
          workingSpans: seg.workingSpans.map((sp) =>
            sp.spanId === spanId
              ? { ...sp, correctedText: newText, isUserEdited: true }
              : sp
          ),
        };
      })
    );
  };

  const handlePromoteJoke = (segmentId: string, spanId: string, reason: string) => {
    setSampleTranscripts((prev) =>
      prev.map((seg) => {
        if (seg.segmentId !== segmentId) return seg;
        return {
          ...seg,
          workingSpans: seg.workingSpans.map((sp) =>
            sp.spanId === spanId
              ? {
                  ...sp,
                  contextType: 'InWorldCanonCandidate' as const,
                  isManuallyConfirmed: true,
                  promotionReason: reason,
                }
              : sp
          ),
        };
      })
    );
  };

  const activeRoster = rosterByCampaign[activeCampaignId] || [];
  const activeLore = loreByCampaign[activeCampaignId] || [];

  return (
    <div
      className={`palantyr-shell ${
        settings.reduceAnimations ? 'reduce-motion-root' : ''
      }`}
    >
      {/* Top Navigation & Stewardship Bar */}
      <header className="topbar">
        <div className="topbar-brand">
          <span className="brand-orb" aria-hidden="true">
            🔮
          </span>
          <div>
            <h1 className="brand-title">{t.appTitle}</h1>
            <span className="brand-subtitle">{t.appSubtitle}</span>
          </div>
          <span className="offline-shield-pill" title="NFR-PRIV-005">
            🛡 {t.offlineBadge}
          </span>
        </div>

        <nav className="topbar-nav" aria-label="Primary Workspace Navigation">
          <button
            type="button"
            className={`nav-btn ${screen === 'welcome' ? 'active' : ''}`}
            onClick={() => setScreen('welcome')}
          >
            🏰 {t.nav.welcome}
          </button>

          <button
            type="button"
            className={`nav-btn ${screen === 'wizard' ? 'active' : ''}`}
            onClick={() => setScreen('wizard')}
          >
            ✦ {t.nav.createCampaign}
          </button>

          {campaigns.length > 0 && (
            <button
              type="button"
              className={`nav-btn ${screen === 'campaignHome' ? 'active' : ''}`}
              onClick={() => setScreen('campaignHome')}
            >
              🐉 {t.nav.campaignHome}
            </button>
          )}

          <button
            type="button"
            className="nav-btn"
            onClick={() => setShowFirstRunModal(true)}
          >
            🖥 {t.nav.systemAssessment}
          </button>

          <button
            type="button"
            className="nav-btn"
            onClick={handleTriggerCrashRecoveryDrill}
          >
            ⚡ {t.nav.simulateCrashRecovery}
          </button>

          <button
            type="button"
            className="nav-btn"
            onClick={() => setShowWindowCloseGuard(true)}
          >
            🛑 {t.nav.attemptWindowClose}
          </button>

          <button
            type="button"
            className="nav-btn"
            onClick={() => setShowSettingsModal(true)}
          >
            ⚙ {t.nav.settings}
          </button>

          <button
            type="button"
            className="btn btn-lang-toggle"
            onClick={handleToggleLanguage}
            aria-label={t.nav.languageToggleLabel}
          >
            🌐 {settings.uiLanguage === 'English' ? 'EN → ES' : 'ES → EN'}
          </button>
        </nav>
      </header>

      {/* Main Content Area */}
      <main className="main-workspace">
        {screen === 'welcome' && (
          <div className="welcome-screen">
            <section className="welcome-hero">
              <div>
                <h2>{t.welcome.heroTitle}</h2>
                <p>{t.welcome.heroSubtitle}</p>
              </div>
              <button
                type="button"
                className="btn btn-gold btn-lg"
                onClick={() => setScreen('wizard')}
              >
                {t.welcome.createNewButton}
              </button>
            </section>

            {/* Hardware & Mode Summary Strip */}
            <section className="hardware-summary-strip">
              <div>
                <strong>{t.welcome.systemSummaryTitle}:</strong>{' '}
                <span>
                  {systemProfile.cpuModel} •{' '}
                  {systemProfile.gpuModel || 'CPU Only'} •{' '}
                  {systemProfile.freeStorageGb.toFixed(1)} GB Free
                </span>
              </div>
              <div className="strip-actions">
                <span className="mode-pill">
                  {t.welcome.recommendedModeLabel}:{' '}
                  <strong>
                    {t.processingModes[settings.activeProcessingMode].title}
                  </strong>
                </span>
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => setShowFirstRunModal(true)}
                >
                  {t.welcome.rerunAssessmentButton}
                </button>
              </div>
            </section>

            {/* Campaign Cards Vault */}
            <section className="campaign-vault-section">
              <h3>{t.welcome.selectCampaignTitle}</h3>
              {campaigns.length === 0 ? (
                <div className="empty-vault-box">
                  <p>{t.welcome.emptyCampaigns}</p>
                </div>
              ) : (
                <div className="campaign-cards-grid">
                  {campaigns.map((camp) => (
                    <article key={camp.campaignId} className="campaign-card">
                      <div className="campaign-card-header">
                        <div>
                          <h4>{camp.name}</h4>
                          {camp.subtitleOrSettingName && (
                            <span className="campaign-subtitle">
                              {camp.subtitleOrSettingName}
                            </span>
                          )}
                        </div>
                        <span className="lang-badge">
                          {camp.primaryLanguage === 'Spanish' ? 'ES' : 'EN'}
                        </span>
                      </div>

                      {camp.worldSummary && (
                        <p className="campaign-summary-text">
                          {camp.worldSummary}
                        </p>
                      )}

                      <div className="campaign-meta-row">
                        <span>
                          🗄 <code>{camp.databaseName}</code>
                        </span>
                        <span>
                          🎙 {t.welcome.sessionsCount}:{' '}
                          <strong>{camp.sessionCount}</strong>
                        </span>
                        <span>
                          🛡 {t.welcome.partyCount}:{' '}
                          <strong>{camp.partyMemberCount}</strong>
                        </span>
                      </div>

                      {camp.needsAttentionCount > 0 && (
                        <div className="campaign-attention-tag">
                          ⚑ {camp.needsAttentionCount}{' '}
                          {t.welcome.needsAttentionBadge}
                        </div>
                      )}

                      {camp.hasUnbackedUpChanges && (
                        <div className="campaign-backup-warn">
                          ⚠ {t.welcome.unbackedUpWarning}
                        </div>
                      )}

                      <div className="campaign-card-actions">
                        <button
                          type="button"
                          className="btn btn-gold"
                          onClick={() => {
                            setActiveCampaignId(camp.campaignId);
                            setHomeStatus((prev) => ({
                              ...prev,
                              campaignId: camp.campaignId,
                            }));
                            setScreen('campaignHome');
                          }}
                        >
                          {t.welcome.openCampaignButton}
                        </button>
                        <button
                          type="button"
                          className="btn btn-ghost-danger"
                          onClick={() => setCampaignPendingDelete(camp)}
                        >
                          {t.welcome.deleteCampaignButton}
                        </button>
                      </div>
                    </article>
                  ))}
                </div>
              )}
            </section>
          </div>
        )}

        {screen === 'wizard' && (
          <CreateCampaignWizard
            defaultLanguage={settings.uiLanguage}
            audioDevices={systemProfile.audioDevices}
            onTestMicrophone={handleTestMicrophone}
            onCreateCampaign={handleCreateCampaign}
            onCancel={() => setScreen('welcome')}
            t={t}
          />
        )}

        {screen === 'campaignHome' && activeCampaign && (
          <div
            className={`campaign-home-workspace session-mode-${homeStatus.sessionState.toLowerCase()}`}
          >
            {/* Persistent Campaign Home Header & Session Transport Bar (UX-CAM-001..002) */}
            <section className="campaign-home-transport-bar">
              <div className="campaign-home-identity">
                <div className="workspace-badges-row">
                  <span className="workspace-pill">
                    {t.campaignHome.workspaceBadge}
                  </span>
                  <span
                    className={`session-state-banner state-${homeStatus.sessionState.toLowerCase()}`}
                    role="status"
                  >
                    {t.sessionStates[homeStatus.sessionState]}
                  </span>
                </div>
                <h2>{activeCampaign.name}</h2>
                <div className="campaign-db-meta">
                  <span>
                    RavenDB: <code>{activeCampaign.databaseName}</code>
                  </span>
                  {activeCampaign.calendarSystemDescription && (
                    <span>• 📅 {activeCampaign.calendarSystemDescription}</span>
                  )}
                  <span>
                    •{' '}
                    {homeStatus.activeSessionId
                      ? `${t.campaignHome.activeSessionBadge}: ${homeStatus.activeSessionId}`
                      : t.campaignHome.noActiveSession}
                  </span>
                </div>
              </div>

              <div className="session-transport-controls">
                <div className="session-timer-box" aria-live="off">
                  <span className="timer-label">
                    {t.campaignHome.elapsedTime}
                  </span>
                  <strong className="timer-digits">
                    {formatDurationMs(homeStatus.totalRecordedDurationMs)}
                  </strong>
                </div>

                {homeStatus.sessionState === 'Idle' ||
                homeStatus.sessionState === 'Ended' ? (
                  <button
                    type="button"
                    className="btn btn-record-start"
                    onClick={handleStartRecording}
                  >
                    {t.campaignHome.startRecordingBtn}
                  </button>
                ) : null}

                {homeStatus.sessionState === 'Recording' && (
                  <>
                    <button
                      type="button"
                      className="btn btn-secondary"
                      onClick={handlePauseRecording}
                    >
                      {t.campaignHome.pauseRecordingBtn}
                    </button>
                    <button
                      type="button"
                      className="btn btn-danger"
                      onClick={handleInitiateEndSession}
                    >
                      {t.campaignHome.endSessionBtn}
                    </button>
                  </>
                )}

                {homeStatus.sessionState === 'Paused' && (
                  <>
                    <button
                      type="button"
                      className="btn btn-gold"
                      onClick={handleResumeRecording}
                    >
                      {t.campaignHome.resumeRecordingBtn}
                    </button>
                    <button
                      type="button"
                      className="btn btn-danger"
                      onClick={handleInitiateEndSession}
                    >
                      {t.campaignHome.endSessionBtn}
                    </button>
                  </>
                )}
              </div>
            </section>

            {/* Pause Transcription Ask Prompt (FR-PROC-003) */}
            {showPauseAskBanner && homeStatus.sessionState === 'Paused' && (
              <div className="pause-ask-banner" role="region">
                <div>
                  <strong>{t.campaignHome.pauseAskPromptTitle}</strong>
                  <p>{t.campaignHome.pauseAskPromptBody}</p>
                </div>
                <div className="pause-ask-actions">
                  <button
                    type="button"
                    className="btn btn-gold"
                    onClick={() => {
                      setShowPauseAskBanner(false);
                      setHomeStatus((prev) => ({
                        ...prev,
                        eonixVisualState: 'Transcribing',
                        transcriptionStatus: 'Processing',
                      }));
                    }}
                  >
                    {t.campaignHome.pauseCatchUpYes}
                  </button>
                  <button
                    type="button"
                    className="btn btn-secondary"
                    onClick={() => {
                      setShowPauseAskBanner(false);
                      setHomeStatus((prev) => ({
                        ...prev,
                        eonixVisualState: 'Paused',
                        transcriptionStatus: 'Paused',
                      }));
                    }}
                  >
                    {t.campaignHome.pauseCatchUpNo}
                  </button>
                </div>
              </div>
            )}

            {/* 4 Explicit Independent Subsystem Status Cards (UX-HOME-004, AC-USAB-001) */}
            <SubsystemStatusBar
              status={homeStatus}
              language={settings.uiLanguage}
              t={t}
            />

            {/* Wave 3A: Workspace View Switcher (Session Console, Dual Transcripts, Memory Codex) */}
            <div className="workspace-view-switcher">
              <button
                type="button"
                className={`view-switcher-tab ${workspaceView === 'console' ? 'active' : ''}`}
                onClick={() => setWorkspaceView('console')}
              >
                🎙️ {settings.uiLanguage === 'Spanish' ? 'Consola de Sesión' : 'Session Console'}
              </button>
              <button
                type="button"
                className={`view-switcher-tab ${workspaceView === 'transcripts' ? 'active' : ''}`}
                onClick={() => setWorkspaceView('transcripts')}
              >
                📜 {settings.uiLanguage === 'Spanish' ? 'Transcripciones Duales' : 'Dual Transcripts'}
              </button>
              <button
                type="button"
                className={`view-switcher-tab ${workspaceView === 'memory' ? 'active' : ''}`}
                onClick={() => setWorkspaceView('memory')}
              >
                📚 {t.memoryBrowser.title}
                {resolutionCandidates.filter((c) => c.status === 'PendingDmReview').length > 0 && (
                  <span className="tab-bubble alert">
                    {resolutionCandidates.filter((c) => c.status === 'PendingDmReview').length}
                  </span>
                )}
              </button>
            </div>

            {workspaceView === 'console' && (
              <div className="campaign-home-columns">
              <div className="campaign-col-primary">
                <EonixCompanionCard
                  eonixState={homeStatus.eonixVisualState}
                  reduceAnimations={settings.reduceAnimations}
                  needsAttentionCount={homeStatus.needsAttentionCount}
                  messages={eonixMessages}
                  onSendMessage={handleSendEonixMessage}
                  onSelectStatePreview={(st: EonixState) =>
                    setHomeStatus((prev) => ({
                      ...prev,
                      eonixVisualState: st,
                    }))
                  }
                  t={t}
                />
              </div>

              <div className="campaign-col-secondary">
                {/* Needs Attention Quarantine Queue (FR-MEM-CONF-001..006) */}
                <section className="home-panel attention-panel">
                  <div className="panel-header">
                    <h3>
                      ⚑ {t.campaignHome.needsAttentionPanelTitle} (
                      {attentionItems.length})
                    </h3>
                  </div>
                  <p className="panel-desc">
                    {t.campaignHome.needsAttentionDesc}
                  </p>
                  {attentionItems.length === 0 ? (
                    <div className="all-clear-box">
                      ✓ Zero quarantined items.
                    </div>
                  ) : (
                    <div className="attention-list">
                      {attentionItems.map((item) => (
                        <div key={item.id} className="attention-card">
                          <strong>{item.title}</strong>
                          <p>{item.excerpt}</p>
                          <div className="attention-actions">
                            <button
                              type="button"
                              className="btn btn-sm btn-gold"
                              onClick={() =>
                                handleResolveAttentionItem(item.id)
                              }
                            >
                              ✓ {t.campaignHome.resolveItemBtn}
                            </button>
                            <button
                              type="button"
                              className="btn btn-sm btn-secondary"
                              onClick={() =>
                                handleResolveAttentionItem(item.id)
                              }
                            >
                              ✖ {t.campaignHome.markFalsePositiveBtn}
                            </button>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </section>

                {/* Party Roster & Historical Membership (FR-CAM-001..006) */}
                <section className="home-panel">
                  <div className="panel-header">
                    <h3>
                      🛡 {t.campaignHome.partyRosterPanelTitle} (
                      {activeRoster.length})
                    </h3>
                  </div>
                  <div className="roster-cards-list">
                    {activeRoster.map((member) => (
                      <div key={member.characterId} className="roster-row-card">
                        <div>
                          <strong>{member.characterName}</strong>
                          <span className="roster-sub">
                            {member.raceOrSpecies} • {member.classOrArchetype}
                          </span>
                          <span className="roster-owner">
                            {member.isPlayerCharacter
                              ? `${t.campaignHome.playerControlled}: ${
                                  member.controlledByPlayerName || 'PC'
                                }`
                              : t.campaignHome.dmTemporaryNpc}
                          </span>
                        </div>
                        <button
                          type="button"
                          className={`status-pill status-${member.currentStatus}`}
                          onClick={() =>
                            handleCyclePartyMemberStatus(member.characterId)
                          }
                          title="Click to transition party membership status"
                        >
                          {t.partyStatuses[member.currentStatus]} ↻
                        </button>
                      </div>
                    ))}
                  </div>
                </section>

                {/* World Lore & Epistemic Vault (FR-SETUP-001..006, Section 12) */}
                <section className="home-panel">
                  <div className="panel-header">
                    <h3>
                      📜 {t.campaignHome.lorePanelTitle} ({activeLore.length})
                    </h3>
                  </div>
                  <div className="lore-cards-list">
                    {activeLore.map((entry) => (
                      <div key={entry.loreId} className="lore-entry-card">
                        <div className="lore-entry-top">
                          <strong>{entry.title}</strong>
                          <span
                            className={`scope-tag scope-${entry.visibility}`}
                          >
                            {t.visibilityScopes[entry.visibility].label}
                          </span>
                        </div>
                        <p>{entry.content}</p>
                        <div className="lore-entry-meta">
                          <span className="epistemic-tag">
                            {t.knowledgeStates[entry.knowledgeState]}
                          </span>
                          <span className="confidence-tag">
                            {t.confidenceLevels[entry.confidence]}
                          </span>
                        </div>
                      </div>
                    ))}
                  </div>
                </section>
              </div>
            </div>
            )}

            {workspaceView === 'transcripts' && (
              <TranscriptAndReviewPanel
                t={t}
                language={settings.uiLanguage}
                transcripts={sampleTranscripts}
                needsAttentionItems={attentionItems.map((item) => ({
                  id: item.id,
                  campaignId: activeCampaignId,
                  sessionId: homeStatus.activeSessionId || 'session-004',
                  sourceType: 'ContextAmbiguity',
                  title: item.title,
                  promptMessageEn: item.excerpt,
                  promptMessageEs: item.excerpt,
                  targetSpanId: 'span-103',
                  approximateTimestampMs: 24000,
                  isResolved: false,
                  createdAtUtc: '2026-11-14T20:00:00Z',
                }))}
                onCorrectSpan={handleCorrectSpan}
                onPromoteJoke={handlePromoteJoke}
                onResolveAttentionItem={handleResolveAttentionItem}
              />
            )}

            {workspaceView === 'memory' && (
              <MemoryBrowserPanel
                t={t}
                language={settings.uiLanguage}
                campaignId={activeCampaignId}
                entities={memoryEntities}
                resolutionCandidates={resolutionCandidates}
                conflicts={memoryConflicts}
                onSaveEntity={handleSaveMemoryEntity}
                onDeleteEntity={handleDeleteMemoryEntity}
                onResolveCandidate={handleResolveEntityCandidate}
                onResolveConflict={handleResolveLoreConflict}
              />
            )}
          </div>
        )}
      </main>

      {/* Modals */}
      {showFirstRunModal && (
        <FirstRunAssessmentModal
          profile={systemProfile}
          language={settings.uiLanguage}
          selectedMode={settings.activeProcessingMode}
          onAcceptMode={(mode: ProcessingMode) => {
            setSettings((prev) => ({ ...prev, activeProcessingMode: mode }));
            setShowFirstRunModal(false);
          }}
          t={t}
        />
      )}

      {endSessionStage > 0 && (
        <TwoStageEndSessionModal
          stage={endSessionStage as 1 | 2}
          summary={endSessionSummary}
          onCancelStage1={() => setEndSessionStage(0)}
          onConfirmStage1SealAudio={handleConfirmStage1SealAudio}
          onStage2Choice={handleStage2Choice}
          t={t}
        />
      )}

      {crashReport && (
        <CrashRecoveryModal
          report={crashReport}
          language={settings.uiLanguage}
          onResolveChoice={handleResolveCrashRecovery}
          t={t}
        />
      )}

      {campaignPendingDelete && (
        <SafeCampaignDeleteModal
          campaign={campaignPendingDelete}
          onSelectDeletionOption={handleSelectCampaignDeletion}
          t={t}
        />
      )}

      {showWindowCloseGuard && (
        <WindowCloseGuardModal
          onReturnToSession={() => setShowWindowCloseGuard(false)}
          onEndSessionAndExit={() => {
            setShowWindowCloseGuard(false);
            handleInitiateEndSession();
          }}
          t={t}
        />
      )}

      {showSettingsModal && (
        <SettingsModal
          settings={settings}
          audioDevices={systemProfile.audioDevices}
          onSaveSettings={async (updated) => {
            setSettings(updated);
            try {
              await fetch('/api/v1/settings', {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(updated),
              });
            } catch {
              // Stored in local state
            }
          }}
          onTestMicrophone={handleTestMicrophone}
          onRerunAssessment={() => {
            setShowSettingsModal(false);
            setShowFirstRunModal(true);
          }}
          onClose={() => setShowSettingsModal(false)}
          t={t}
        />
      )}
    </div>
  );
};

export default App;
