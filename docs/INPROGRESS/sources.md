# Источники миграции (Loading/Bootstrap)

Реестр внешних проектов, откуда переносится код в MyBookstore.

---

## Research (Pet#1)

- **Путь:** `C:\Projects\Research\Assets`
- **Точка входа загрузки:** `C:\Projects\Research\Assets\Game\Core\Bootstrap\Bootstrap.cs`
- **Ветка / коммит:** _TODO — зафиксировать перед началом переноса_
- **Роль:** источник «недостающих кусков» того же стиля (VContainer + IAsyncStartable). Закрывает TODO-стабы в `GameLoadingVContainerBindings`, `UiSystemVContainerBindings`, `AnalyticsVContainerBindings`, `GameInstaller`.

---

## Pet#2

- **Путь:** `C:\Projects\heroes\Assets\Root`
- **Точка входа загрузки:** `C:\Projects\heroes\Assets\Root\Bootstrapper.cs`
- **Ветка / коммит:** по последнему состоянию
- **Роль (Phase 2 — текущий скоуп):** donor для debug-флагов старта (`UseDebugSimplifiedStart`, `_useDebugFeatures`-маршрут). Остальное (LoadLocationCommand, GameStateService, метрики) — отложено по решению пользователя.

---

## Правила работы с источниками

- Не коммитить чужой код «как есть» без читки — каждый файл проходит ревью на нерезолвящиеся `using`-и и лишние зависимости.
- Каждая порция переноса = отдельный коммит с пометкой источника в теле: `Source: Research @ <commit>` или `Source: Pet#2 @ <commit>`.
- Имена классов из источника **не переименовывать на лету** — это ломает сравнение и поиск.
