# Система сохранений (Save System)

## Обзор архитектуры

Система сохранений построена на многослойной архитектуре с несколькими бэкендами хранения. Проектировалась под production mobile game с большой аудиторией.

---

## Основные компоненты

### Оркестрация

| Класс | Путь | Назначение |
|-------|------|-----------|
| `SaveController` | `Assets/Core/Saves/SaveController.cs` | Главный менеджер, управляет всеми Entry и хранилищами |
| `SaveLoop` | `Assets/Game/Saves/SaveLoop.cs` | Автосейв каждые 10 секунд |
| `SaveBehaviour` | `Assets/Shared/CoreLogic/CoreSaves/Core/SaveBehaviour.cs` | Включает/отключает flush во время критических операций |
| `FileStorage` | `Assets/Shared/CoreLogic/CoreSaves/Core/Storage/External/FileStorage.cs` | Локальное файловое хранилище с атомарной записью и бэкапом |
| `FileSavePaths` | `Assets/Shared/CoreLogic/CoreSaves/Core/Storage/External/FileSavePaths.cs` | Управляет путями файлов сейва (main, tmp, backup) |

---

## Локальное хранение данных

### Entry-based подход

95+ классов-наследников `AbstractEntry<TData>` (`Assets/Shared/CoreLogic/CoreSaves/Core/AbstractEntry.cs`), каждый отвечает за свой аспект прогресса:

- `PlayerEntry`, `HeroesEntry`, `FormationsEntry`
- `ResourcesEntry`, `QuestsEntry`, `BattleEntry`
- и другие (~95 типов)

### Цепочка сериализации

```
Game objects
    ↓
AbstractEntry<TData>
    ↓
MessagePack binary (SavesMessagePackSerializer)
    ↓
SaveDataEntry { bytes[] + CRC }
    ↓
Dictionary<string, SaveDataEntry>
    ↓
FileStorage (атомарная запись: tmp → rename → main)
```

### Хранилища

| Хранилище | Описание |
|-----------|---------|
| `MemoryStorage` | In-memory кэш всех данных сейва |
| `FileStorage` | Диск, атомарная запись через tmp-файл |
| `BackupStorage` | Автоматический бэкап при успешной загрузке |

**Сериализация:** MessagePack бинарный формат — компактнее JSON, быстрее при чтении/записи.

---

## Отправка данных в облако

### Облачные платформы

#### iOS iCloud
- Файл: `Assets/Core/IosCloud/IosCloud.cs`
- Использует Prime31 iCloud SDK, Document Store
- Асинхронная загрузка/сохранение через события

#### AWS S3
- Файл: `Assets/Core/Aws/AwsCloud.cs`
- Ключ объекта: `{ApiKey}/{SocialId}/{SaveName}`
- Команды: `LoadBytesAwsS3Command`, `SendBytesAwsS3Command`, `DeleteObjectAwsS3Command`

### Упаковка перед отправкой в облако

`CloudSavePacker` (`Assets/Core/CloudSave/CloudSavePacker.cs`):

```
| Header | MetaSize | MetaBytes | DataSize | DataBytes |
```

Метаданные включают: Gold, Real (валюта), Level, Experience, Timestamp, SpecialFlags.

**Throttle:** `CloudSaveService` (`Assets/Core/CloudSave/CloudSaveService.cs`) ограничивает частоту облачных сохранений через настраиваемый таймаут.

### Команды облака

| Команда | Описание |
|---------|---------|
| `SendToCloudCommand` | Загрузка упакованного сейва с метаданными |
| `LoadFromCloudCommand` | Скачивание и сравнение с локальным сейвом |
| `ChangeCloudCommand` | Смена облачного провайдера |
| `ResetSaveInCloudCommand` | Очистка облачного сейва |

---

## Синхронизация с собственным сервером

### Поток синхронизации

`TryLoadProgressFromServer` (`Assets/Root/Initialization/Server/TryLoadProgressFromServer.cs`):

**При старте игры:**
1. Запрос `PlayerMetaData` с сервера → сравнение timestamp
2. Если сервер новее → `RequestGetSaveData` (сервер отдаёт только дельту по чексуммам)
3. Если клиент новее → `RequestSetSaveData` (загрузка на сервер)

### Data Transfer Objects

| DTO | Описание |
|-----|---------|
| `SaveMetaData` | Gold, Real, Level, Exp, Timestamp, ProgressReset |
| `SaveDataState` | Dictionary entries + metadata + patches + missing entries |
| `SaveDataEntry` | byte[] + CRC для проверки целостности |
| `SaveDataEntryPatch` | Дельта-патчи для оптимизации трафика |

### Данные, которые НЕ отправляются на сервер

- `RequestQueueEntry` — офлайн-очередь запросов
- `ClientServerSyncDataEntry` — мета-данные синхронизации

---

## Режимы сохранения

| Режим | Описание |
|-------|---------|
| `Regular` | Обычный сейв + синк с сервером если доступен |
| `ForceClientOnly` | Только локально, без сервера |
| `Force` | Принудительный сейв + принудительный синк |

---

## Поток инициализации при старте

```
LoadOrCreateLocalSaveCommand
    ↓
GetStaticDataFromServerCommand
    ↓
TryLoadProgressFromServer
    ↓
InitialSyncClientServerDataCommand
```

---

## Оценка системы

### Сложность: Высокая (обоснованная)

Каждый слой решает реальную проблему:

| Проблема | Решение |
|----------|---------|
| Потеря данных при краше | Атомарная запись tmp → rename + бэкап |
| Трафик при синке | Дельта-патчи + CRC чексуммы |
| Мультиплатформенность | Абстракция над iCloud / AWS S3 / своим сервером |
| Консистентность | CRC на каждой Entry, timestamp-based conflict resolution |
| Объём данных | MessagePack вместо JSON — компактный бинарный формат |

### Гибкость: Высокая, но с ограничениями

**Что легко:**
- Добавить новый тип данных — создаёшь новый `AbstractEntry<T>`, он автоматически подхватывается
- Сменить облачный провайдер — есть абстракция, новая реализация подключается без изменения core
- Добавить новое хранилище — через интерфейс

**Что сложно:**
- **Миграция схемы** — MessagePack с CRC не прощает ломающих изменений структуры Entry, нужны версионирование и ручные миграции
- **Дебаггинг** — бинарный формат + несколько слоёв упаковки затрудняют ручную инспекцию сейва
- **Тестирование** — зависимость от сервера, iCloud, AWS усложняет unit-тесты
- **Рефакторинг** — 95+ Entry-классов означает, что изменение базового класса трудоёмко

### Архитектурные решения

**Хорошие:**
- Entry-based подход — чёткое разделение ответственности
- Timestamp + delta sync — разумная экономия трафика
- Atomic write — защита от потери данных при внезапном завершении процесса

**Спорные:**
- Два облачных направления (платформенные сторы + свой сервер) — дублирование логики conflict resolution
- 95+ Entry-классов — может быть симптомом органического роста без рефакторинга
- `SaveLoop` с фиксированным интервалом 10 сек — не учитывает текущую нагрузку и состояние сети

---

## Карта файлов

| Компонент | Путь |
|-----------|------|
| Core Save Controller | `Assets/Core/Saves/SaveController.cs` |
| Save Loop (Auto-save) | `Assets/Game/Saves/SaveLoop.cs` |
| Cloud Service | `Assets/Core/CloudSave/CloudSaveService.cs` |
| Cloud Packer | `Assets/Core/CloudSave/CloudSavePacker.cs` |
| Cloud Commands | `Assets/Core/CloudSave/Commands/` |
| File Storage | `Assets/Shared/CoreLogic/CoreSaves/Core/Storage/External/FileStorage.cs` |
| Entry Base Class | `Assets/Shared/CoreLogic/CoreSaves/Core/AbstractEntry.cs` |
| All Entry Types | `Assets/Shared/CoreLogic/CoreSaves/` |
| Server Sync | `Assets/Root/Initialization/Server/TryLoadProgressFromServer.cs` |
| AWS Cloud | `Assets/Core/Aws/AwsCloud.cs` |
| iOS Cloud | `Assets/Core/IosCloud/IosCloud.cs` |
| Server DTOs | `Assets/Shared/CoreApi.Pure/Server/Users/Players/SaveData/` |
