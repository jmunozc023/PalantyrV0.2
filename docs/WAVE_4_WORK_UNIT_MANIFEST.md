# Wave 4 Work-Unit Manifest (`MANIFEST-WAVE-04`)

> **Manifest ID**: `MANIFEST-WAVE-04`  
> **Target Scope**: Wave 4 — Session Review, Summaries & Eonix Tabletop Assistant (`EPIC-08`, `EPIC-09`, `EPIC-10`)  
> **Capacity Receipt**: `requested: 5` | `accepted: 5` | `active: 5` | `pending: 0`  
> **Reference Architecture**: [PALANTYR_V1_ARCHITECTURE_AND_ADRS.md](file:///F:/PalantyrV2/docs/PALANTYR_V1_ARCHITECTURE_AND_ADRS.md) (`ADR-006`, `ADR-007`, `ADR-008`)

---

## Work Units Table

| Unit ID | Owner Role | Exclusive Owned Paths | Stories Covered | Acceptance Criteria | Stop Condition |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `UNIT-W4-01` | Session Summary & Privacy Gatekeeper Worker | `src/Palantyr.Contracts/SessionSummaryDtos.cs`, `src/Palantyr.Domain/SessionSummaryService.cs`, `src/Palantyr.Domain/SummaryContracts.cs` | `US-08.1`, `US-08.2`, `US-08.3`, `US-08.4`, `US-08.5` | `AC-SUM-001`, `AC-SUM-002`, `AC-COR-001`, `NFR-PRIV-002..003` | Implements `SessionSummaryService` producing context-aware summaries. Strictly enforces pre-generation privacy filtering (`PartySafe` vs `DMFull`) via `QueryVisibilityContext`. Propagates DM corrections across memory documents with `DMCorrection` provenance. No delegation. |
| `UNIT-W4-02` | Needs Attention & Session Review Worker | `src/Palantyr.Contracts/SessionReviewDtos.cs`, `src/Palantyr.Domain/SessionReviewService.cs`, `src/Palantyr.Audio/AudioSlicerService.cs` | `US-09.1`, `US-09.2`, `US-09.3`, `US-09.4`, `US-09.5` | `AC-REV-001`, `AC-REV-002`, `FR-VAL-002` | Implements Needs Attention prioritization (`Resolve ASAP` for identity/names), "one-scroll-at-a-time" conversational resolution, 10-second audio clip slicing around timestamps, and compact batch Full Review (`Yes` / `No` / `Confirm All`) with immediate autosave and Undo. No delegation. |
| `UNIT-W4-03` | Eonix Grounded RAG & Teaching Worker | `src/Palantyr.Contracts/EonixDtos.cs`, `src/Palantyr.Domain/EonixAssistantService.cs`, `src/Palantyr.Domain/EonixContracts.cs` | `US-10.1`, `US-10.2`, `US-10.3`, `US-10.4`, `US-10.5` | `AC-EONIX-001`, `AC-EONIX-002`, `AC-EONIX-003`, `FR-EONIX-013` | Implements ADR-007 hybrid retrieval (Exact lookup, Spatiotemporal traversal, semantic ranking) with strict knowledge scope filtering (`DM Full` vs `Party Knowledge`). Enforces grounding contract (refuses to invent facts when missing, answers "how sure are you?" with confidence scores), conversational teaching, and evidence sources view. No delegation. |
| `UNIT-W4-04` | RavenDB Storage Expansion & Host APIs Worker | `src/Palantyr.Domain/StoreContracts.cs`, `src/Palantyr.Data.Raven/RavenCampaignMemoryStore.cs`, `src/Palantyr.Host/Program.cs` | `US-08.1`..`US-10.5` | Full Wave 4 Host & Data Coverage | Expands `RavenCampaignMemoryStore` to persist summaries, review snapshots, and audio clips; exposes typed Minimal API endpoints for summary generation, review batching, audio slicing, and Eonix chat/teaching. No delegation. |
| `UNIT-W4-05` | UI Summary Modal, Eonix Evidence & Test Gate | `src/palantyr-ui/src/components/SessionSummaryModal.tsx`, `src/palantyr-ui/src/components/EonixCompanionCard.tsx`, `src/palantyr-ui/src/App.tsx`, `src/palantyr-ui/src/i18n/translations.ts`, `tests/Palantyr.UnitTests/Wave4Tests.cs` | Full EPIC-08, EPIC-09, EPIC-10 Gate | `AC-SUM-001..002`, `AC-REV-001..002`, `AC-EONIX-001..003` | Delivers bilingual UI for Summary generation, Eonix knowledge-scope toggle, Evidence citation drawer with transcript navigation, and comprehensive xUnit test suite (`Wave4Tests.cs`). No delegation. |

---

## Acceptance Criteria & Quality Gates

1. **`AC-SUM-001` & `AC-SUM-002`**:
   - `PartySafe` summary generation runs strictly over `PartySafeMemoryView` / `VisibilityScope.PartyKnown` sources. Zero `DMPrivate` secrets or ambiguous items can appear in party output.
   - Summaries link causal events and distinguish narrative recap from direct DM notes.
2. **`AC-REV-001` & `AC-REV-002`**:
   - Needs Attention items resolved one at a time via natural language or quick buttons.
   - Batch Full Review allows confirming or rejecting memories with autosave and Undo.
   - 10-second audio slice around span timestamp is playable during review.
3. **`AC-EONIX-001`, `AC-EONIX-002`, `AC-EONIX-003`**:
   - Always-available private text interface (never triggered by audio, never speaks aloud).
   - Grounded responses cite evidence (`EvidenceSourceDto`). Deterministically admits when info is unknown.
   - Phrasing reflects Confidence and Knowledge State (distinguishes Canon Truth vs Party Belief vs Rumor).
   - Conversational teaching updates memory and records DM provenance.
4. **Build & Test Invariants**:
   - All tests pass via `dotnet test F:\PalantyrV2\Palantyr.sln`.
   - UI compiles with 0 errors via `npm --prefix F:\PalantyrV2\src\palantyr-ui run build`.


---

## Execution Status & Evidence Receipt

- **`UNIT-W4-01` (Session Summary & Privacy Gatekeeper)**: **`COMPLETED`**
  - Files: [`src/Palantyr.Contracts/SessionSummaryDtos.cs`](file:///F:/PalantyrV2/src/Palantyr.Contracts/SessionSummaryDtos.cs), [`src/Palantyr.Domain/SessionSummaryService.cs`](file:///F:/PalantyrV2/src/Palantyr.Domain/SessionSummaryService.cs), [`src/Palantyr.Domain/SummaryContracts.cs`](file:///F:/PalantyrV2/src/Palantyr.Domain/SummaryContracts.cs).
  - Evidence: `AC-SUM-001` & `AC-SUM-002` verified in `Wave4Tests.cs`. Pre-generation privacy gatekeeper (`ADR-006`) rigorously excludes DM secrets prior to prompt ingestion.
- **`UNIT-W4-02` (Needs Attention & Session Review)**: **`COMPLETED`**
  - Files: [`src/Palantyr.Contracts/SessionReviewDtos.cs`](file:///F:/PalantyrV2/src/Palantyr.Contracts/SessionReviewDtos.cs), [`src/Palantyr.Domain/SessionReviewService.cs`](file:///F:/PalantyrV2/src/Palantyr.Domain/SessionReviewService.cs), [`src/Palantyr.Audio/WavRiffHeaderHelper.cs`](file:///F:/PalantyrV2/src/Palantyr.Audio/WavRiffHeaderHelper.cs).
  - Evidence: `AC-REV-001` (single item resolution) and `AC-REV-002` (batch review autosave + deep snapshot Undo) verified in `Wave4Tests.cs`. Audio slicing logic supports 10-second context playback.
- **`UNIT-W4-03` (Eonix Grounded RAG & Teaching)**: **`COMPLETED`**
  - Files: [`src/Palantyr.Contracts/EonixDtos.cs`](file:///F:/PalantyrV2/src/Palantyr.Contracts/EonixDtos.cs), [`src/Palantyr.Domain/EonixAssistantService.cs`](file:///F:/PalantyrV2/src/Palantyr.Domain/EonixAssistantService.cs), [`src/Palantyr.Domain/EonixContracts.cs`](file:///F:/PalantyrV2/src/Palantyr.Domain/EonixContracts.cs).
  - Evidence: `AC-EONIX-001` (grounded citations), `AC-EONIX-002` (honest unknown admission), `AC-EONIX-003` (scope isolation), `AC-EONIX-004` (conversational teaching) verified in `Wave4Tests.cs`.
- **`UNIT-W4-04` (RavenDB Storage Expansion & Host APIs)**: **`COMPLETED`**
  - Files: [`src/Palantyr.Domain/StoreContracts.cs`](file:///F:/PalantyrV2/src/Palantyr.Domain/StoreContracts.cs), [`src/Palantyr.Data.Raven/RavenCampaignMemoryStore.cs`](file:///F:/PalantyrV2/src/Palantyr.Data.Raven/RavenCampaignMemoryStore.cs), [`src/Palantyr.Host/Program.cs`](file:///F:/PalantyrV2/src/Palantyr.Host/Program.cs).
  - Evidence: Atomic summary persistence, REST API endpoints mapped (`/api/campaigns/{campaignId}/sessions/{sessionId}/summaries`, `/reviews`, `/audio-clip`, `/eonix/query`, `/eonix/teach`).
- **`UNIT-W4-05` (UI Summary Modal, Eonix Evidence & Test Gate)**: **`COMPLETED`**
  - Files: [`src/palantyr-ui/src/components/SessionSummaryModal.tsx`](file:///F:/PalantyrV2/src/palantyr-ui/src/components/SessionSummaryModal.tsx), [`src/palantyr-ui/src/components/TranscriptAndReviewPanel.tsx`](file:///F:/PalantyrV2/src/palantyr-ui/src/components/TranscriptAndReviewPanel.tsx), [`src/palantyr-ui/src/components/EonixCompanionCard.tsx`](file:///F:/PalantyrV2/src/palantyr-ui/src/components/EonixCompanionCard.tsx), [`src/palantyr-ui/src/App.tsx`](file:///F:/PalantyrV2/src/palantyr-ui/src/App.tsx), [`src/palantyr-ui/src/i18n/translations.ts`](file:///F:/PalantyrV2/src/palantyr-ui/src/i18n/translations.ts), [`tests/Palantyr.UnitTests/Wave4Tests.cs`](file:///F:/PalantyrV2/tests/Palantyr.UnitTests/Wave4Tests.cs).
  - Evidence: `dotnet test F:\PalantyrV2\Palantyr.sln` -> **31/31 passed (0 failed)**; `npm run build` -> **0 errors (clean Vite production bundle)**.


---

## Post-Review Remediation & Final Verification

- **Review Lens Alignment**:
  - **Aurora Midori** (`aurora`): Reviewed backend and frontend architecture against `ADR-006`, `ADR-007`, and `ADR-008`. Identified 7 findings (privacy boundaries on CharacterScoped queries, summary correction persistence, ADR-008 GPU mutex injection, tombstone persistence in attention resolution, and frontend port/scope wiring).
  - **Usagi** (`usagi`): Validated acceptance criteria and boundary coverage. Identified 2 high severity items (correction metadata wipe, missing UI trigger) and 4 boundary test gaps.
- **Remediation Completed (One Focused Round)**:
  1. `SessionSummaryService.cs`: Merged corrections into existing summaries, preserving `SessionId`, `Mode`, `Title`, `KeyEvents`, `MajorDiscoveries`, and `OutstandingQuestions`. Integrated `IRuntimePriorityScheduler` for ADR-008 Single-Active-GPU-Worker mutex coordination.
  2. `EonixAssistantService.cs`: Routed `CharacterScoped` queries strictly through `QueryPartySafeMemoryAsync` with `ScopedCharacterIds` to exclude DM secrets. Integrated `IRuntimePriorityScheduler`. Added Spanish copula support (`" es "`, `" son "`, `" era "`, `" eran "`) and inverted punctuation trimming (`¿`, `¡`).
  3. `SessionReviewService.cs`: Implemented `RejectWithTombstone` resolution tombstone persistence (`EntityResolutionStatus.Dismissed`) and deep cloning for character, location, and journey facets, with bounded in-memory undo cache.
  4. `src/palantyr-ui/src/App.tsx`: Added visible "📜 Session Chronicle" trigger button in `session-transport-controls`, fixed API endpoint URLs to `/api/v1/...` with POST body, and wired `handleSendEonixMessage` to backend `/api/v1/campaigns/{id}/eonix/chat` and `/eonix/teach`.
  5. `EonixCompanionCard.tsx`: Connected knowledge scope toggle pill to message submission.
  6. `Wave4Tests.cs`: Added 5 comprehensive boundary test cases (`AC_SUM_003` empty memory, `AC_COR_002` correction round-trip, `AC_EONIX_005` Spanish teaching, `AC_REV_003` invalid undo token, `AC_EONIX_006` character-scoped privacy filter).
- **Final Test Verification Evidence**:
  - `dotnet test F:\PalantyrV2\Palantyr.sln`: **36 passed, 0 failed, 0 skipped** across all test suites.
  - `npm --prefix F:\PalantyrV2\src\palantyr-ui run build`: **0 errors, clean Vite production bundle** (`dist/assets/index-XZYUb-Vk.js`).
