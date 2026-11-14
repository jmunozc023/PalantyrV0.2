# Palantyr

> **Palantyr is the second memory of a DM.**  
> *The DM runs the game. Palantyr remembers it.*

Palantyr is a Windows-first, offline-first Campaign Memory Keeper for Dungeon Masters and Game Masters. It records tabletop RPG sessions safely to progressive local audio segments, transcribes them locally (`en` / `es`), filters out-of-character table talk and jokes from campaign canon, extracts structured Campaign Memory across sessions, validates continuity-critical ambiguities with the DM, and provides grounded retrieval through **Eonix**.

---

## Technology Stack (V1)

- **Backend & Desktop Host**: `.NET 10` (Windows-first, `WebView2` desktop container, `WASAPI` progressive audio engine)
- **Database & Hybrid Search**: `RavenDB Embedded` (Database-per-Campaign isolation, Corax multilingual `en`/`es` Full-Text Search, native HNSW Vector Search, immutable Document Revisions, and `.ravendbdump` backups)
- **Frontend UI**: `React` + `TypeScript` + `Vite` + `i18next` (Bilingual English & Spanish)
- **Local AI Sidecars**: Replaceable adapter contracts (`ITranscriptionEngine`, `IContextClassifier`, `IMemoryExtractor`, `IEonixAssistant`) backed by local `faster-whisper`/`whisper.cpp` and `llama.cpp`/`Ollama` workers

---

## Documentation

- [Initial Project Plan, EPICs & User Stories](docs/PALANTYR_V1_EPICS_AND_USER_STORIES.md)
- [System Architecture & Technical Design v1.0 (ADRs)](docs/PALANTYR_V1_ARCHITECTURE_AND_ADRS.md)
