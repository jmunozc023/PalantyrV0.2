namespace Palantyr.Contracts;

/// <summary>
/// Supported UI, transcription, and campaign languages (FR-TRN-003).
/// </summary>
public enum PalantyrLanguage
{
    English = 0,
    Spanish = 1
}

/// <summary>
/// Processing modes recommended by System Assessment or selected by DM (Section 6.1).
/// </summary>
public enum ProcessingMode
{
    LiveBalanced = 0,
    RecordingOnly = 1,
    ManualDeferred = 2
}

/// <summary>
/// Pause transcription preference (FR-PROC-003).
/// </summary>
public enum PauseTranscriptionBehavior
{
    Never = 0,
    Ask = 1,
    Always = 2
}

/// <summary>
/// Campaign Home session state (UX-CAM-002).
/// </summary>
public enum SessionRecordingState
{
    Idle = 0,
    Recording = 1,
    Paused = 2,
    Ended = 3,
    Recovering = 4
}

/// <summary>
/// Independent post-session/background processing status (FR-END-003, UX-HOME-004).
/// </summary>
public enum SubsystemProcessingState
{
    Idle = 0,
    Saved = 1,
    Pending = 2,
    Processing = 3,
    Paused = 4,
    Completed = 5,
    Failed = 6
}

/// <summary>
/// Eonix visual & operational state feedback (UX-HOME-003).
/// </summary>
public enum EonixState
{
    Idle = 0,
    ListeningRecording = 1,
    Transcribing = 2,
    MemoryProcessing = 3,
    Thinking = 4,
    Answering = 5,
    NeedsAttention = 6,
    Paused = 7,
    Error = 8
}

/// <summary>
/// Party character membership states (FR-CAM-002).
/// </summary>
public enum PartyMembershipStatus
{
    Active = 0,
    Temporary = 1,
    Former = 2,
    Deceased = 3,
    Inactive = 4
}

/// <summary>
/// Conversation context classification types (FR-CTX-002).
/// </summary>
public enum ConversationContextType
{
    InWorldCanonCandidate = 0,
    OutOfCharacterTableTalk = 1,
    HypotheticalOrJoke = 2,
    RulesOrMeta = 3,
    Ambiguous = 4
}

/// <summary>
/// In-world epistemic state (Section 12, FR-MEM-CONF-006).
/// </summary>
public enum KnowledgeState
{
    CampaignTruth = 0,
    DMPrivateTruth = 1,
    PartyBelief = 2,
    PlayerOrCharacterTheory = 3,
    ClaimOrRumor = 4,
    UnknownOrUnresolved = 5,
    DisprovenOrSupersededBelief = 6
}

/// <summary>
/// Extraction/interpretation confidence level (FR-MEM-CONF-002).
/// </summary>
public enum ConfidenceLevel
{
    DMConfirmed = 0,
    HighConfidence = 1,
    MediumConfidence = 2,
    LowOrUncertain = 3
}

/// <summary>
/// Visibility boundary for pre-generation privacy filtering (FR-SETUP-006, FR-SUM-001..005, ADR-006).
/// </summary>
public enum VisibilityScope
{
    GeneralWorld = 0,
    PartyKnown = 1,
    CharacterScoped = 2,
    DMPrivate = 3,
    Ambiguous = 4
}

/// <summary>
/// Source provenance types (FR-MEM-DM-007, FR-MEM-LORE-002).
/// </summary>
public enum ProvenanceSourceType
{
    CampaignSetup = 0,
    Transcript = 1,
    SessionSummary = 2,
    DMCorrection = 3,
    DMDirectNote = 4,
    DMVoiceNote = 5,
    DMConversationalTeaching = 6
}

/// <summary>
/// Universal Memory Entity categories (Sections 8-22).
/// </summary>
public enum MemoryEntityType
{
    Character = 0,
    Location = 1,
    Object = 2,
    CraftingEvent = 3,
    Organization = 4,
    Quest = 5,
    SocialEvent = 6,
    PartyConversation = 7,
    CombatEvent = 8,
    WorldLore = 9,
    JourneyOrStay = 10
}

/// <summary>
/// Segment status inside the authoritative .pseg Write-Ahead Log (ADR-003).
/// </summary>
public enum AudioSegmentWalStatus
{
    Active = 0,
    ClosedSafe = 1,
    RecoveredFromCrash = 2,
    CorruptedUnrecoverable = 3,
    CleanedUpAfterProcessing = 4
}

/// <summary>
/// Runtime priority tier enforced during active sessions (FR-PERF-001, NFR-PERF-002).
/// </summary>
public enum RuntimePriorityTier
{
    CriticalRecordingAndAudioSave = 1,
    HighInteractiveEonixQuery = 2,
    DeferrableBackgroundProcessing = 3
}