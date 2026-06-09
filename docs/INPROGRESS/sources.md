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

- **Путь:** _TODO — будет указан перед стартом Phase 2_
- **Ветка / коммит:** _TODO_
- **Роль:** источник оркестрации и UX-слоя загрузки. Donor для Command/Phases, LoadingScreen, Debug-флагов, GameStateService, LoadLocationCommand.

---

## Правила работы с источниками

- Не коммитить чужой код «как есть» без читки — каждый файл проходит ревью на нерезолвящиеся `using`-и и лишние зависимости.
- Каждая порция переноса = отдельный коммит с пометкой источника в теле: `Source: Research @ <commit>` или `Source: Pet#2 @ <commit>`.
- Имена классов из источника **не переименовывать на лету** — это ломает сравнение и поиск.
