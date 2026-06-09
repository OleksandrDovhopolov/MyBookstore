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

## Connect PostgreSQL and Redis on Railway
- **Notion:** https://app.notion.com/p/370511859db381a0ba78f43c49b8acaf
- **Commit:** _n/a — infra setup via Railway dashboard; secrets in Railway env vars (no git artifacts on the client side)_
- **Date:** 2026-06-09
- **Summary:** Provisioned PostgreSQL and Redis as Railway plugins next to the existing .NET server deployment. Postgres is the primary store (player save via `/api/v1/save/global`, versioned `configs` table for the Config Server API, future entitlements/IAP-validation), Redis is the cache / sessions / scratch layer to offload Postgres. Connection strings live in Railway env vars per environment (dev/prod), not in the repo. End-to-end working — verified via the existing save sync flow and the public/admin Config Server API. Service cards in Notion (🔧 Настройки сервисов): PostgreSQL → Настроено, Redis → Настроено, Railway already Настроено. Deferred: concrete feature-specific migrations (account/entitlements/IAP), Postgres backup strategy, Redis TTL policy, Railway billing monitoring — followups noted on the corresponding service cards.

## Data-driven configs (BookConfig, LocationConfig, RequestConfig, EventConfig)
- **Notion:** https://app.notion.com/p/370511859db3810c8a1bf9d144aea22c
- **Commits:** `14c1c8d` (Firebase RC stub) → `33b29b6` (config system core) → `aff4d6b` (server + Firebase RC override) → `243cdf5` (connect bootstrap) → `1bcdc9e` (clear server snapshot menu) → `371fc9c` (editor window) → `2c22f47` → `5cacb94` (final) — `update config`
- **Date:** 2026-06-09
- **Summary:** End-to-end data-driven config system across three layers — base source (`LocalFolderConfigSource` for editor / `ServerConfigSource` for runtime with manifest + delta-by-ETag + disk snapshot fallback), override layer (`RemoteConfigOverrideSource` merging Firebase RC `cfg_<fileName>` partials over base behind `BOOKSTORE_FIREBASE_RC` define), and service contract (`IConfigsService.Get/TryGet/GetAsync/IsExists/GetAll`) with lazy per-type deserialization. POCO models for Book/Location/Request/Event with `[ConfigFile]` attribute. Production transport reuses `Game.Http`/`AbstractServiceCommand` (manifest + `If-None-Match` + 304 + ETag canonicalization). DI via VContainer — `ConfigsVContainerBindings.RegisterConfigs` in `BootstrapInstaller`, warmup ordered by `ConfigsWarmupEntryPoint` (RC `InitializeAsync` → `IConfigsService.WarmupAsync`). Editor publishing tool (`Configs.Editor` Editor-only assembly, menu `Tools/Configs/Editor Window`): connection foldout with Basic-auth credentials in EditorPrefs, item list with search/Add/Duplicate/Delete, typed form for books + generic JObject form for other sections + Raw JSON fallback, validation gating Publish, optimistic-concurrency via `If-Match`/`bootstrap`/412-conflict modal, separate history window with lazy-fetched `/versions/{v}` content + Rollback + Promote dev→prod, Copy Error / Copy Current JSON, no password/Authorization leakage in logs. Architecture decision in `docs/adr/0002-config-system-architecture.md` with extension paths. Specs at `docs/CONFIG_CACHE_SYSTEM.md`, `docs/CONFIG_EDITOR_WINDOW_MVP_SPEC.md`; backend contract mirror at `docs/CONFIG_SERVER_API.md`. Removed obsolete `docs/config-storage-analysis.md` (described another project). Backend admin endpoints (`PUT`, `history`, `rollback`, `promote`, `versions/{v}`) implemented by backend developer separately per spec. Deferred follow-ups tracked as Notion tasks: `Bundled defaults недоступны в build` (High / Sprint 3) and `BookDuneProbe race with RC warmup` (Low / Backlog). Post-MVP per spec §16: auto-merge on 412, inline diff viewer, typed forms for locations/requests/events, batch import/export, schema validation per section.
