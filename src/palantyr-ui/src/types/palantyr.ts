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


// ============================================================================

export interface ProvenanceRef {
  sourceType: ProvenanceSourceType;
  sessionId?: string | null;
  segmentId?: string | null;
  spanId?: string | null;
  startTimestampMs?: number | null;
  endTimestampMs?: number | null;
  recordedAtUtc: string;
  noteOrContext?: string | null;
}

export interface FirstAppearanceRef {
  sessionId: string;
  segmentId?: string | null;
  spanId?: string | null;
  approximateTimestampMs?: number | null;
  recordedAtUtc: string;
}

export interface MemoryHistoryItemDto {
  historyItemId: string;
  description: string;
  knowledgeState: KnowledgeState;
  confidence: ConfidenceLevel;
  visibility: VisibilityScope;
  validFromSessionId?: string | null;
  validToSessionId?: string | null;
  relativeTimeContext?: string | null;
  supersededByHistoryItemId?: string | null;
  recordedAtUtc: string;
  authorizedCharacterIds?: string[] | null;
}

// Wave 3A: Core Campaign Memory Graph, Spatiotemporal & Authority Types
// ============================================================================

export type LocationType =
  | 'Settlement'
  | 'Structure'
  | 'Room'
  | 'GeographicFeature'
  | 'Region'
  | 'TransitWaypoint';

export interface CharacterStatementDto {
  statementId: string;
  sessionId: string;
  segmentId?: string | null;
  spanId?: string | null;
  timestampMs?: number | null;
  summaryText: string;
  targetSubject?: string | null;
  isByCharacter: boolean;
  visibility: VisibilityScope;
}

export interface CharacterFacetDto {
  raceOrSpecies?: string | null;
  physicalDescription?: string | null;
  roleOrArchetype?: string | null;
  affiliations: string[];
  statementsBy: CharacterStatementDto[];
  statementsAbout: CharacterStatementDto[];
}

export interface TransitLocationRefDto {
  originLocationId: string;
  destinationLocationId: string;
  routeName?: string | null;
  transitDurationDays?: number | null;
}

export interface LocationFacetDto {
  locationType: LocationType;
  parentLocationId?: string | null;
  enclosedChildIds: string[];
  isTransitPoint: boolean;
  transitEndpoints?: TransitLocationRefDto | null;
}

export interface RelativeDayTransitionDto {
  transitionId: string;
  sessionId: string;
  segmentId?: string | null;
  fromRelativeDay: number;
  toRelativeDay: number;
  narrativeMarkerText: string;
  detectedAtUtc: string;
}

export interface KeyMomentRefDto {
  momentId: string;
  sessionId: string;
  timestampMs?: number | null;
  summaryText: string;
  relativeDayNumber?: number | null;
  participants: string[];
}

export interface JourneyFacetDto {
  parentJourneyId?: string | null;
  originLocationId?: string | null;
  destinationLocationId?: string | null;
  totalEstimatedDays?: number | null;
  currentRelativeDay?: number | null;
  relativeTimeMarker?: string | null;
  departureCampaignDate?: string | null;
  arrivalCampaignDate?: string | null;
  dayTransitions: RelativeDayTransitionDto[];
  keyMoments: KeyMomentRefDto[];
}

export interface LocationHierarchyNodeDto {
  locationId: string;
  canonicalName: string;
  locationType: LocationType;
  parentLocationId?: string | null;
  breadcrumbPath: string;
  children: LocationHierarchyNodeDto[];
}

export type EntityResolutionStatus =
  | 'PendingDmReview'
  | 'LinkedToExisting'
  | 'CreatedNewEntity'
  | 'Dismissed';

export type ResolutionMatchReason =
  | 'ExactAliasMatch'
  | 'PhoneticSimilarity'
  | 'LevenshteinDistance'
  | 'KnownSpeechVariant';

export interface CandidateMatchDto {
  entityId: string;
  canonicalName: string;
  entityType: MemoryEntityType;
  matchScore: number;
  matchReason: ResolutionMatchReason;
}

export interface EntityResolutionCandidateDto {
  candidateId: string;
  campaignId: string;
  observedSurfaceName: string;
  contextSnippet?: string | null;
  sourceSessionId?: string | null;
  sourceSegmentId?: string | null;
  sourceSpanId?: string | null;
  detectedAtUtc: string;
  status: EntityResolutionStatus;
  potentialMatches: CandidateMatchDto[];
  resolvedEntityId?: string | null;
  resolutionDecision?: string | null;
}

export interface ResolveCandidateRequest {
  action: 'LinkToExisting' | 'CreateNew' | 'AcousticCorrection' | 'Dismiss';
  targetEntityId?: string | null;
  newEntityCanonicalName?: string | null;
  registerAsSpeechVariant?: boolean;
  dmNotes?: string | null;
  decidedBy?: string;
}

export type ConflictType =
  | 'FactualContradiction'
  | 'TemporalEvolution'
  | 'DivergentBelief';

export type ConflictResolutionState =
  | 'Unresolved'
  | 'ResolvedSuperseded'
  | 'ResolvedCoexistingBeliefs'
  | 'ResolvedDmRuling';

export interface CompetingAssertionDto {
  assertionId: string;
  assertionText: string;
  sourceRef?: ProvenanceRef | null;
  knowledgeState: KnowledgeState;
  confidence: ConfidenceLevel;
  recordedAtUtc: string;
}

export interface MemoryConflictDto {
  conflictId: string;
  campaignId: string;
  entityId: string;
  conflictType: ConflictType;
  summary: string;
  competingAssertions: CompetingAssertionDto[];
  state: ConflictResolutionState;
  dmRuling?: string | null;
  resolvedAtUtc?: string | null;
}

export interface ResolveConflictRequest {
  resolution: ConflictResolutionState;
  winningAssertionId?: string | null;
  dmRulingNote?: string | null;
  decidedBy?: string;
}

export interface MemoryEntityDto {
  id: string;
  entityType: MemoryEntityType;
  canonicalName: string;
  aliases: string[];
  titles: string[];
  recognitionVariants: string[];
  summaryDescription: string;
  dmPrivateNotes?: string | null;
  visibility: VisibilityScope;
  authorizedCharacterIds: string[];
  knowledgeState: KnowledgeState;
  confidence: ConfidenceLevel;
  firstAppearance?: FirstAppearanceRef | null;
  characterFacet?: CharacterFacetDto | null;
  locationFacet?: LocationFacetDto | null;
  journeyFacet?: JourneyFacetDto | null;
  stateAndBeliefHistory: MemoryHistoryItemDto[];
  sources: ProvenanceRef[];
}

export interface CreateMemoryEntityRequest {
  entityType: MemoryEntityType;
  canonicalName: string;
  aliases?: string[];
  titles?: string[];
  recognitionVariants?: string[];
  summaryDescription?: string;
  dmPrivateNotes?: string;
  visibility?: VisibilityScope;
  authorizedCharacterIds?: string[];
  knowledgeState?: KnowledgeState;
  confidence?: ConfidenceLevel;
  firstAppearance?: FirstAppearanceRef;
  characterFacet?: CharacterFacetDto;
  locationFacet?: LocationFacetDto;
  journeyFacet?: JourneyFacetDto;
}

export interface UpdateMemoryEntityRequest {
  canonicalName?: string;
  aliases?: string[];
  titles?: string[];
  recognitionVariants?: string[];
  summaryDescription?: string;
  dmPrivateNotes?: string;
  visibility?: VisibilityScope;
  authorizedCharacterIds?: string[];
  knowledgeState?: KnowledgeState;
  confidence?: ConfidenceLevel;
  characterFacet?: CharacterFacetDto;
  locationFacet?: LocationFacetDto;
  journeyFacet?: JourneyFacetDto;
}

// Wave 4: Session Summaries, Needs Attention & Eonix Tabletop Assistant (EPIC-08, EPIC-09, EPIC-10)

export type SummaryVisibilityMode = 'PartySafe' | 'DmFull';

export interface SummaryKeyEventDto {
  eventId: string;
  description: string;
  relativeTimeContext?: string | null;
  locationId?: string | null;
  participants: string[];
  visibility: VisibilityScope;
}

export interface SummaryDiscoveryDto {
  subject: string;
  description: string;
  knowledgeState: KnowledgeState;
  confidence: ConfidenceLevel;
}

export interface SessionSummaryDto {
  summaryId: string;
  campaignId: string;
  sessionId: string;
  mode: SummaryVisibilityMode;
  title: string;
  executiveRecap: string;
  keyEvents: SummaryKeyEventDto[];
  majorDiscoveries: SummaryDiscoveryDto[];
  outstandingQuestions: string[];
  dmPrivateNotes?: string | null;
  generatedAtUtc: string;
  promptTemplateVersion: string;
  sources: ProvenanceRef[];
}

export interface GenerateSummaryRequest {
  mode: SummaryVisibilityMode;
  targetLanguage?: PalantyrLanguage;
  includeDmPrivateNotes?: boolean;
  customFocusNotes?: string | null;
}

export interface UpdateSummaryRequest {
  title?: string | null;
  executiveRecap?: string | null;
  keyEvents?: SummaryKeyEventDto[] | null;
  majorDiscoveries?: SummaryDiscoveryDto[] | null;
  outstandingQuestions?: string[] | null;
  dmPrivateNotes?: string | null;
  dmCorrectionNote?: string | null;
  correctedBy?: string;
}

export type NeedsAttentionAction =
  | 'Confirm'
  | 'RejectWithTombstone'
  | 'DeferOrUnresolved'
  | 'LinkToExisting'
  | 'ClassifyAsJoke'
  | 'PromoteToCanon';

export interface ResolveAttentionItemRequest {
  action: NeedsAttentionAction;
  targetEntityId?: string | null;
  correctionText?: string | null;
  notes?: string | null;
  resolvedBy?: string;
}

export interface MemoryReviewItemDto {
  entityId: string;
  canonicalName: string;
  entityType: MemoryEntityType;
  claimOrStatement: string;
  knowledgeState: KnowledgeState;
  confidence: ConfidenceLevel;
  sourceSessionId?: string | null;
  timestampMs?: number | null;
  isConfirmed?: boolean;
  isRejected?: boolean;
}

export interface SingleReviewDecisionDto {
  entityId: string;
  isConfirmed: boolean;
  isRejected: boolean;
  correctionText?: string | null;
  rejectionReason?: string | null;
}

export interface BatchReviewRequest {
  sessionId: string;
  decisions: SingleReviewDecisionDto[];
  decidedBy?: string;
}

export interface ReviewUndoToken {
  undoTokenId: string;
  campaignId: string;
  sessionId: string;
  executedAtUtc: string;
  affectedEntityIds: string[];
}

export interface AudioClipRequest {
  sessionId: string;
  segmentId: string;
  targetTimestampMs: number;
  preRollSeconds?: number;
  postRollSeconds?: number;
}

export interface AudioClipResult {
  clipFilePath: string;
  startTimestampMs: number;
  endTimestampMs: number;
  durationSeconds: number;
}

export type EonixScope = 'CampaignDmFull' | 'PartyKnowledge' | 'CharacterScoped';

export interface EvidenceSourceDto {
  sourceId: string;
  sourceType: ProvenanceSourceType;
  entityId?: string | null;
  sessionId?: string | null;
  segmentId?: string | null;
  spanId?: string | null;
  timestampMs?: number | null;
  snippet: string;
  knowledgeState: KnowledgeState;
  confidence: ConfidenceLevel;
}

export interface EonixChatQueryRequest {
  query: string;
  scope?: EonixScope;
  scopeCharacterId?: string | null;
  language?: PalantyrLanguage;
}

export interface EonixChatQueryResponse {
  answer: string;
  isGrounded: boolean;
  confidence: ConfidenceLevel;
  confidenceScore: number;
  sources: EvidenceSourceDto[];
  scopeApplied: EonixScope;
}

export interface EonixTeachRequest {
  taughtStatement: string;
  targetEntityName?: string | null;
  isDmSecret?: boolean;
  clarificationResponse?: string | null;
  taughtBy?: string;
}

export interface EonixTeachResponse {
  success: boolean;
  targetEntityId: string;
  targetCanonicalName: string;
  knowledgeState: KnowledgeState;
  visibility: VisibilityScope;
  requiresClarification: boolean;
  clarificationQuestion?: string | null;
}
