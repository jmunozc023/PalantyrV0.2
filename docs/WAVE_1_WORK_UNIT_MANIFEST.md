# Wave 1 Work-Unit Manifest (`MANIFEST-WAVE-01`)

> **Manifest ID**: `MANIFEST-WAVE-01`  
> **Target Scope**: Wave 1 — First Vertical Slice (`EPIC-00` Contracts, `EPIC-01` System Assessment & Priority Runtime, `EPIC-02` Campaign Management & Campaign Home, `EPIC-03` Resilient Audio Recording & Crash Recovery)  
> **Capacity Receipt**: `requested: 4` | `accepted: 4` | `active: 4` | `pending: 0`

---

## Work Units Table

| Unit ID | Owner Role | Exclusive Owned Paths | Stories Covered | Acceptance Criteria | Stop Condition |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `UNIT-W1-01` | Audio & WAL Recovery Worker | `src/Palantyr.Audio/**` | `US-03.1`..`US-03.5` | `AC-REC-001..003`, `AC-END-001..002`, `AC-DATA-002`, `AC-REL-002`, `AC-REL-004` | All `Palantyr.Audio` classes implemented with `.pseg` authoritative WAL, 2s flush, RIFF header crash repair, 15m rollover, storage watchdog, and 10s clip slicer. No delegation. |
| `UNIT-W1-02` | RavenDB & Campaign Store Worker | `src/Palantyr.Data.Raven/**` | `US-00.1`, `US-02.1`..`US-02.4`, `US-06.1` (Store), `US-11.1` (Backup) | `AC-DATA-001`, `AC-DATA-003`, `AC-DATA-006`, `AC-SUM-002` | `Palantyr.Data.Raven` implements `ISystemStore` & `ICampaignMemoryStore` with RavenDB Embedded memory caps (`Kaede Finding 2`), `PartySafeMemoryView` sub-collection projection (`Kaede Finding 3`), and safe campaign backup/delete. No delegation. |
| `UNIT-W1-03` | Runtime Scheduler, Host API & Unit Tests Worker | `src/Palantyr.AI.Orchestration/**`, `src/Palantyr.Host/**`, `tests/Palantyr.UnitTests/**` | `US-00.2`..`US-00.3`, `US-01.1`..`US-01.4` | `AC-SYS-001`, `AC-PERF-001..002`, `AC-DATA-004..005`, `AC-PRIV-002` | Implements `RuntimePriorityScheduler` (with Single-GPU Mutex), `WindowsSystemAssessmentService`, `.NET 10` loopback API (`Palantyr.Host`), redacted diagnostic logger, and comprehensive `Palantyr.UnitTests`. No delegation. |
| `UNIT-W1-04` | Bilingual React UI Worker (`EN`/`ES`) | `src/palantyr-ui/**` | `US-01.1`, `US-01.4`, `US-02.1`..`US-02.5`, `US-03.2`..`US-03.5` | `AC-USAB-001`, `AC-USAB-003`, `AC-USAB-004` | Implements complete React + TypeScript + Vite frontend with `en`/`es` i18n, First-Run Assessment, Welcome + Safe Delete, Create Campaign Wizard, Persistent Campaign Home with 4 explicit status indicators, Two-Stage End Session, Crash Recovery Modal, and Settings. No delegation. |
