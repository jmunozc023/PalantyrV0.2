# Wave 3A Work-Unit Manifest (`MANIFEST-WAVE-03A`)

> **Manifest ID**: `MANIFEST-WAVE-03A`  
> **Target Scope**: Wave 3A — Core Campaign Memory Graph (`EPIC-06`, Stories `US-06.1` through `US-06.7`)  
> **Capacity Receipt**: `requested: 5` | `accepted: 5` | `active: 0` | `completed: 5`  
> **Status**: **`COMPLETED & VERIFIED`**  
> **Reference Architecture**: [PALANTYR_V1_ARCHITECTURE_AND_ADRS.md](file:///F:/PalantyrV2/docs/PALANTYR_V1_ARCHITECTURE_AND_ADRS.md) (`ADR-004`, `ADR-006`, `ADR-007`)

---

## Work Units Table

| Unit ID | Owner Role | Exclusive Owned Paths | Stories Covered | Acceptance Criteria | Status | Stop Condition & Evidence |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `UNIT-W3A-01` | Core Memory & Entity Model Worker | `src/Palantyr.Contracts/CoreMemoryDtos.cs`, `src/Palantyr.Domain/CoreEntities.cs`, `src/Palantyr.Domain/MemoryModels.cs` | `US-06.1`, `US-06.2` | `AC-MEM-001`, `AC-MEM-002`, `AC-MEM-003`, `AC-ENT-001` | **DONE** | Implements domain models for Universal Memory, Epistemic Knowledge States (`FR-MEM-KNOW-001..004`), Confidence levels (`FR-MEM-CONF-001..010`), and Character Memory with in-world aliases, titles, and speech-to-text recognition variants cleanly separated (`FR-MEM-CHAR-001..002`). Sub-item `VisibilityScope` stripping verified in `AC_MEM_003`. |
| `UNIT-W3A-02` | Location Hierarchy & Journey / Relative Time Worker | `src/Palantyr.Contracts/SpatiotemporalDtos.cs`, `src/Palantyr.Domain/SpatiotemporalModels.cs` | `US-06.3`, `US-06.4` | `AC-TIME-001`, `FR-MEM-LOC-001..003`, `FR-MEM-TIME-001..004` | **DONE** | Implements Location containment hierarchies (`World > Region > Settlement > Structure > Room`), transit event routing without false endpoint attribution, nested events, journeys, relative day transition detection, and real session time vs campaign calendar date decoupling. Verified in `AC_TIME_001`. |
| `UNIT-W3A-03` | Entity Resolution & Conflict Authority Engine Worker | `src/Palantyr.Contracts/ResolutionAndConflictDtos.cs`, `src/Palantyr.Domain/AuthorityEngine.cs` | `US-06.5`, `US-06.6` | `AC-ENT-001`, `AC-ENT-002`, `FR-MEM-CONFLICT-001..007` | **DONE** | Implements `CampaignMemoryService` with deterministic candidate similarity scoring (Levenshtein + token distance), **strict no-auto-merge enforcement**, candidate queueing for DM review, retroactive reference repair propagator, and contradiction detection distinguishing temporal evolution from factual conflicts. Verified in `AC_ENT_002` and `FR_MEM_CONFLICT_001`. |
| `UNIT-W3A-04` | RavenDB Per-Campaign Memory Store & Index Worker | `src/Palantyr.Data.Raven/RavenCampaignMemoryStore.cs` | `US-06.1`..`US-06.7` | `AC-MEM-001`, `AC-DATA-001`, `AC-DATA-006` | **DONE** | Expands `RavenCampaignMemoryStore` to persist, query, and mutate core entities, location hierarchy trees, journey timelines, resolution queues, and conflict ledgers. Document revision and CRUD endpoints fully implemented and covered by unit tests. |
| `UNIT-W3A-05` | Host APIs, UI Memory Browser & Wave 3A Test Gate | `src/Palantyr.Host/Program.cs`, `src/palantyr-ui/src/components/MemoryBrowserPanel.tsx`, `src/palantyr-ui/src/App.tsx`, `src/palantyr-ui/src/i18n/translations.ts`, `tests/Palantyr.UnitTests/Wave3ATests.cs` | `US-06.7`, Full EPIC-06 Gate | Full Wave 3A Verification Gate (`AC-MEM-001..003`, `AC-ENT-001..002`, `AC-TIME-001`) | **DONE** | Exposes typed REST endpoints for Memory CRUD, Location Hierarchy, Timeline, Resolution Candidates, and Conflicts; integrates bilingual (`en`/`es`) Memory Browser into Campaign Home with direct manual CRUD; delivers 22 passing tests in `dotnet test` and clean UI build in `vite build`. |

---

## Final Gate Verification Evidence

1. **Backend Test Suite Execution**:
   - Command: `dotnet test F:\PalantyrV2\Palantyr.sln`
   - Result: `Passed! - Failed: 0, Passed: 22, Skipped: 0, Total: 22, Duration: 1 s - Palantyr.UnitTests.dll (net10.0)`
   - All Wave 1, Wave 2, and Wave 3A vertical slice tests passed cleanly.
2. **Frontend Production Build**:
   - Command: `npm --prefix F:\PalantyrV2\src\palantyr-ui run build`
   - Result: `✓ built in 2.81s (0 TypeScript errors, clean bundle generated)`.
