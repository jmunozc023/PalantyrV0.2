# Wave 2 Work-Unit Manifest (`MANIFEST-WAVE-02`)

> **Manifest ID**: `MANIFEST-WAVE-02`  
> **Target Scope**: Wave 2 — Local Transcription Pipeline (`EPIC-04`) & Conversation Context Classification (`EPIC-05`)  
> **Capacity Receipt**: `requested: 4` | `accepted: 4` | `active: 4` | `pending: 0`  
> **Reference Architecture**: [PALANTYR_V1_ARCHITECTURE_AND_ADRS.md](file:///f:/PalantyrV2/docs/PALANTYR_V1_ARCHITECTURE_AND_ADRS.md) (`ADR-004`, `ADR-005`, `ADR-008`)

---

## Work Units Table

| Unit ID | Owner Role | Exclusive Owned Paths | Stories Covered | Acceptance Criteria | Stop Condition |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `UNIT-W2-01` | Transcription Adapter & Queue Worker | `src/Palantyr.AI.Orchestration/Transcription/**`, `src/Palantyr.Contracts/Interfaces.cs` | `US-04.1`, `US-04.2`, `US-04.3`, `US-04.5` | `AC-TRN-001`, `AC-TRN-002`, `AC-REL-001`, `AC-REL-003`, `AC-MAINT-001` | Implements `ITranscriptionEngine` (bilingual `en`/`es` Whisper adapter with custom glossary injection) and `ITranscriptionQueue` with priority/throttling awareness and pause-time catch-up (`Never`, `Ask`, `Always`). No delegation. |
| `UNIT-W2-02` | Dual Transcript & Needs Attention Store Worker | `src/Palantyr.Domain/**`, `src/Palantyr.Data.Raven/**` | `US-04.4`, `US-05.3` | `AC-DATA-001`, `AC-REV-001` (Store foundation) | Implements `TranscriptSegmentDocument` with dual-layer architecture (`MachineUtterances` immutable raw layer vs. `WorkingSpans` mutable corrected layer), `NeedsAttentionItem` persistence in RavenDB per campaign, and DM spelling/semantic corrections. No delegation. |
| `UNIT-W2-03` | Context Classification & Joke-to-Canon Worker | `src/Palantyr.AI.Orchestration/ContextClassification/**` | `US-05.1`, `US-05.2`, `US-05.3`, `US-05.4` | `AC-CTX-001`, `AC-CTX-002`, `AC-CTX-003`, `AC-CTX-004`, `AC-CTX-005` | Implements `TwoStageContextClassifier` for span-level canon classification (`InWorldCanonCandidate`, `OutOfCharacterTableTalk`, `HypotheticalOrJoke`, `RulesOrMeta`, `Ambiguous`), joke-to-canon promotion (`FR-CTX-006`), non-disruptive `Needs Attention` routing for ambiguous spans, and prompt/model lineage attribution. No delegation. |
| `UNIT-W2-04` | Host APIs, UI Status Integration & Wave 2 Tests | `src/Palantyr.Host/**`, `src/palantyr-ui/**`, `tests/Palantyr.UnitTests/**` | `US-04.3`..`US-04.4`, `US-05.1`..`US-05.3` | Full Wave 2 Test Gate Verification | Exposes transcription and context classification endpoints on loopback API, updates UI transcription card real-time states and dual-layer transcript viewer, and delivers comprehensive xUnit test suite (`Wave2Tests.cs`). No delegation. |
