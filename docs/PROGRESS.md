# Task Progress

Log of completed tasks for the MyBookstore project. Source of truth for tasks — Notion (database "Tasks before release (MVP)").
This file — local log of completed work for quick context and commit ↔ task linkage.

Record format:

```
## <Task title>
- **Notion:** <url>
- **Commit:** <short hash> — <message>
- **Date:** YYYY-MM-DD
- **Summary:** brief description
```

---

## Modular architecture skeleton (asmdef)
- **Notion:** https://app.notion.com/p/370511859db38186bbf5c93e68ea430c
- **Commit:** `754fcd1` — add base asmdef folders
- **Date:** 2026-06-08
- **Summary:** Created folders and root asmdef files for modules `Infrastructure`, `Core.Models`, `UIShared` and features `BookSell`, `Shop`, `Quest`, `RewardDrop`, `Inventory`, `IAP`, `Analytics`, `Save`. Existing `Game.Http` and `Game.Commands` moved under `Infrastructure/`. Cross-assembly `references` were not configured (folders are empty placeholders). `UISystem` skipped per decision. Rules — `docs/ASMDEF_RULES.md`.

## Save module (local + versioning + HTTP backend)
- **Notion:** https://app.notion.com/p/370511859db381feba8df9f9abd93fe4
- **Commits:** `c30a123` (V1 skeleton) → `4935b47` → `7fb90cc` → `79e7740` (update save) — final
- **Date:** 2026-06-08
- **Summary:** Implemented `ISaveService` over modular opaque-payload model (`Dictionary<string, ModulePayload>` keyed per feature). Storages: `LocalDiskStorage` with atomic write + `.bak`; `HttpSaveStorage` with write-through cache against `https://gameserver-production-be8b.up.railway.app/api/v1/save/global`. Features: debounce + rate limit (rate-limit applied before semaphore), async hooks (`ISaveHook`), `IDisposable`, `SaveMode` (`Regular`/`ForceLocalOnly`/`ForceWithSync`), `BlockAutosave()` lease, payload telemetry with module-level size breakdown, SHA256 integrity hash + monotonic `Revision`. Round-trip verified end-to-end via `TestStart` smoke test against the production server. Architecture decision recorded in `docs/adr/0001-save-data-modular-payload.md`. Deferred to follow-up tasks: `LocalDiskStorage` stale `.tmp`/`.bak` recovery edge cases, `SaveSyncBootstrap` integration into game bootstrap.

## Firebase service setup (Analytics + Crashlytics)
- **Notion:** https://app.notion.com/p/370511859db381ae8cbeffad30ef1c9e
- **Commits:** `a3350ad` (Git LFS) → `f279c4a` (Firebase SDK 13.12.0 import) → `d0748b8` (.gitignore) → `124747e` (adjust firebase) — final
- **Date:** 2026-06-08
- **Summary:** Registered Android app in Firebase Console (`com.bobak.mybookstore`). Imported Firebase Unity SDK 13.12.0 — `FirebaseAnalytics` + `FirebaseCrashlytics` modules with EDM4U 1.2.187. `google-services.json` wired via `FirebaseApp.androidlib` + desktop fallback. Added required Android permissions (`INTERNET`, `ACCESS_NETWORK_STATE`, `AD_ID`). Secrets ignored in `.gitignore`. Git LFS configured for native binaries (`.a`, `.aar`, `.srcaar`, `.bundle`, `.so`, `.dll`, `.dylib`, `.unitypackage`) — separate doc in Notion → Инженерия → Git LFS. Implemented `RemoteConfigLoader.EnsureDependenciesAsync` over `FirebaseApp.CheckAndFixDependenciesAsync`; connection verified via temporary MonoBehaviour tester (now removed). Deferred: Firebase Remote Config SDK import + `FetchAndActivateAsync` impl, Unity Gaming Services, DI registration of the loader in `BootstrapInstaller`, analytics event plan + real `LogEvent` calls.
