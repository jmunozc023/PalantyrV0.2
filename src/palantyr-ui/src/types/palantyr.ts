/**
 * Palantyr V1 TypeScript Contracts
 * Mirrors Palantyr.Contracts C# DTOs and Enums 1:1 (Enums.cs, SystemAndCampaignDtos.cs, AudioAndSessionDtos.cs).
 */

export type PalantyrLanguage = 'English' | 'Spanish';

export type ProcessingMode =
  | 'LiveBalanced'
  | 'RecordingOnly'
  | 'ManualDeferred';

export type PauseTranscriptionBehavior = 'Never' | 'Ask' | 'Always';

export type SessionRecordingState =
  | 'Idle'
  | 'Recording'
  | 'Paused'
  | 'Ended'
  | 'Recovering';

export type SubsystemProcessingState =
  | 'Idle'
  | 'Saved'
  | 'Pending'
  | 'Processing'
  | 'Paused'
  | 'Completed'
  | 'Failed';

export type EonixState =
  | 'Idle'
  | 'ListeningRecording'
  | 'Transcribing'
  | 'MemoryProcessing'
  | 'Thinking'
  | 'Answering'
  | 'NeedsAttention'
  | 'Paused'
  | 'Error';

export type PartyMembershipStatus =
  | 'Active'
  | 'Temporary'
  | 'Former'
  | 'Deceased'
  | 'Inactive';

export type ConversationContextType =
  | 'InWorldCanonCandidate'
  | 'OutOfCharacterTableTalk'
  | 'HypotheticalOrJoke'
  | 'RulesOrMeta'
  | 'Ambiguous';

export type KnowledgeState =
  | 'CampaignTruth'
  | 'DMPrivateTruth'
  | 'PartyBelief'
  | 'PlayerOrCharacterTheory'
  | 'ClaimOrRumor'
  | 'UnknownOrUnresolved'
  | 'DisprovenOrSupersededBelief';

export type ConfidenceLevel =
  | 'DMConfirmed'
  | 'HighConfidence'
  | 'MediumConfidence'
  | 'LowOrUncertain';

export type VisibilityScope =
  | 'GeneralWorld'
  | 'PartyKnown'
  | 'CharacterScoped'
  | 'DMPrivate'
  | 'Ambiguous';

export type ProvenanceSourceType =
  | 'CampaignSetup'
  | 'Transcript'
  | 'SessionSummary'
  | 'DMCorrection'
  | 'DMDirectNote'
  | 'DMVoiceNote'
  | 'DMConversationalTeaching';

export type MemoryEntityType =
  | 'Character'
  | 'Location'
  | 'Object'
  | 'CraftingEvent'
  | 'Organization'
  | 'Quest'
  | 'SocialEvent'
  | 'PartyConversation'
  | 'CombatEvent'
  | 'WorldLore'
  | 'JourneyOrStay';

export type AudioSegmentWalStatus =
  | 'Active'
  | 'ClosedSafe'
  | 'RecoveredFromCrash'
  | 'CorruptedUnrecoverable'
  | 'CleanedUpAfterProcessing';

export type RuntimePriorityTier =
  | 'CriticalRecordingAndAudioSave'
  | 'HighInteractiveEonixQuery'
  | 'DeferrableBackgroundProcessing';

export type CrashRecoveryChoice =
  | 'ContinueSession'
  | 'EndSessionAndProcess'
  | 'CloseSafelyAndStartNew';

export type CampaignDeletionOption =
  | 'CreateBackupAndDelete'
  | 'DeleteWithoutBackup'
  | 'Cancel';

export interface AudioInputDeviceInfo {
  deviceId: string;
  friendlyName: string;
  channels: number;
  sampleRateHz: number;
  isDefault: boolean;
  isAvailable: boolean;
}

export interface MicrophoneValidationResult {
  deviceId: string;
  isAvailable: boolean;
  hasDetectableSignal: boolean;
  peakLevelDb: number;
  diagnosticMessageEn: string;
  diagnosticMessageEs: string;
}

export interface SystemAssessmentProfile {
  id: string;
  assessedAtUtc: string;
  windowsVersion: string;
  cpuModel: string;
  logicalCores: number;
  totalRamGb: number;
  availableRamGb: number;
  gpuModel: string | null;
  dedicatedVramGb: number;
  freeStorageGb: number;
  hasCompatibleGpuAcceleration: boolean;
  recommendedMode: ProcessingMode;
  recommendationReasonEn: string;
  recommendationReasonEs: string;
  audioDevices: AudioInputDeviceInfo[];
}

export interface AppSettingsDto {
  uiLanguage: PalantyrLanguage;
  activeProcessingMode: ProcessingMode;
  pausePreference: PauseTranscriptionBehavior;
  selectedAudioDeviceId: string | null;
  keepOriginalSessionAudio: boolean;
  keepAudioWarningAcknowledged: boolean;
  reduceAnimations: boolean;
  nominalSegmentDurationMinutes: number;
}

export interface PartyMembershipHistoryEntry {
  status: PartyMembershipStatus;
  changedAtUtc: string;
  sessionId: string | null;
  campaignDateContext: string | null;
  reasonNote: string | null;
}

export interface PartyMemberDto {
  characterId: string;
  characterName: string;
  raceOrSpecies: string;
  classOrArchetype: string;
  controlledByPlayerName: string | null;
  isPlayerCharacter: boolean;
  currentStatus: PartyMembershipStatus;
  membershipHistory: PartyMembershipHistoryEntry[];
}

export interface WorldLoreEntryDto {
  loreId: string;
  title: string;
  category: string;
  content: string;
  visibility: VisibilityScope;
  knowledgeState: KnowledgeState;
  confidence: ConfidenceLevel;
  sourceType: ProvenanceSourceType;
  createdAtUtc: string;
}

export interface CreateCampaignRequest {
  name: string;
  subtitleOrSettingName: string | null;
  worldSummary: string | null;
  calendarSystemDescription: string | null;
  primaryLanguage: PalantyrLanguage;
  initialPartyRoster: PartyMemberDto[];
  initialWorldLore: WorldLoreEntryDto[];
  preferredMicrophoneDeviceId: string | null;
}

export interface CampaignSummaryDto {
  campaignId: string;
  databaseName: string;
  name: string;
  subtitleOrSettingName: string | null;
  worldSummary: string | null;
  calendarSystemDescription: string | null;
  primaryLanguage: PalantyrLanguage;
  createdAtUtc: string;
  lastOpenedAtUtc: string;
  lastBackupAtUtc: string | null;
  hasUnbackedUpChanges: boolean;
  sessionCount: number;
  partyMemberCount: number;
  needsAttentionCount: number;
}

export interface AudioSegmentWalEntry {
  sessionId: string;
  segmentId: string;
  sequenceNumber: number;
  filePath: string;
  startedAtUtc: string;
  lastFlushedAtUtc: string;
  flushedPcmBytes: number;
  durationMs: number;
  status: AudioSegmentWalStatus;
  closureReason?: string | null;
}

export interface CampaignHomeStatusDto {
  campaignId: string;
  activeSessionId: string | null;
  sessionState: SessionRecordingState;
  eonixVisualState: EonixState;
  recordingSaveStatus: SubsystemProcessingState;
  transcriptionStatus: SubsystemProcessingState;
  memoryProcessingStatus: SubsystemProcessingState;
  eonixQueryStatus: SubsystemProcessingState;
  totalRecordedDurationMs: number;
  safelyStoredSegmentsCount: number;
  transcribedSegmentsCount: number;
  memoryProcessedSegmentsCount: number;
  needsAttentionCount: number;
  freeStorageGb: number;
  isStorageWarningActive: boolean;
  isBackgroundThrottled: boolean;
  throttlingReasonEn: string | null;
  throttlingReasonEs: string | null;
}

export interface EndSessionSummaryDto {
  campaignId: string;
  sessionId: string;
  totalRecordedDurationMs: number;
  safelyStoredSegments: number;
  transcribedSegments: number;
  memoryProcessedSegments: number;
  needsAttentionItemsCount: number;
  processingRemaining: boolean;
}

export interface CrashRecoveryReportDto {
  campaignId: string;
  sessionId: string;
  interruptedAtUtc: string;
  previouslySafeSegmentsCount: number;
  recoveredSegmentsCount: number;
  unrecoverableSegmentsCount: number;
  totalRecoveredDurationMs: number;
  segments: AudioSegmentWalEntry[];
  summaryMessageEn: string;
  summaryMessageEs: string;
}

export interface EonixChatMessage {
  id: string;
  sender: 'dm' | 'eonix';
  mode: 'ask' | 'teach';
  text: string;
  timestampUtc: string;
  citations?: string[];
  knowledgeState?: KnowledgeState;
  visibility?: VisibilityScope;
}

export interface MachineUtteranceDto {
  utteranceId: string;
  startMs: number;
  endMs: number;
  originalText: string;
  confidenceScore: number;
}

export interface WorkingTranscriptSpanDto {
  spanId: string;
  segmentId: string;
  startMs: number;
  endMs: number;
  originalText: string;
  correctedText: string;
  contextType: ConversationContextType;
  contextConfidence: number;
  isUserEdited?: boolean;
  isManuallyConfirmed?: boolean;
  promotionReason?: string | null;
  confirmedBy?: string | null;
}

export interface TranscriptSegmentDto {
  id: string;
  campaignId: string;
  sessionId: string;
  segmentId: string;
  sequenceNumber: number;
  language: PalantyrLanguage;
  transcribedAtUtc: string;
  machineUtterances: MachineUtteranceDto[];
  workingSpans: WorkingTranscriptSpanDto[];
}

export interface NeedsAttentionItemDto {
  id: string;
  campaignId: string;
  sessionId: string;
  sourceType: string;
  title: string;
  promptMessageEn: string;
  promptMessageEs: string;
  targetSpanId: string;
  approximateTimestampMs: number;
  isResolved: boolean;
  resolutionDecision?: string | null;
  createdAtUtc: string;
  resolvedAtUtc?: string | null;
}

export interface TranscriptionJobStatusDto {
  jobId: string;
  campaignId: string;
  sessionId: string;
  segmentId: string;
  state: SubsystemProcessingState;
  enqueuedAtUtc: string;
  completedAtUtc: string | null;
  retryCount: number;
  errorMessage: string | null;
}
