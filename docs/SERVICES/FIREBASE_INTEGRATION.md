# Firebase Integration — C:\Projects\Research

## Проект Firebase

- **Project ID**: `cardcollection-ae3d5`
- **Realtime DB**: `https://cardcollection-ae3d5-default-rtdb.europe-west1.firebasedatabase.app`
- **Storage Bucket**: `cardcollection-ae3d5.firebasestorage.app`
- **SDK версия**: Unity wrapper 13.9.0

---

## Используемые сервисы

| Сервис | Цель |
|--------|------|
| **Remote Config** | Основное — загрузка конфигурации (ивенты, расписания) |
| **Realtime Database** | Только синхронизация серверного времени (`.info/serverTimeOffset`) |
| **Analytics** | Трекинг событий и пользовательских свойств |
| **Auth** | Настроен, подключён к зависимостям |

---

## Ключевые файлы

| Файл | Роль |
|------|------|
| `Assets/Game/Core/Bootstrap/RemoteConfigLoader.cs` | Инициализация и фетч Remote Config |
| `Assets/Game/Core/Bootstrap/Loading/Operations/FirebaseDependenciesOperation.cs` | Loading-операция: проверка зависимостей Firebase |
| `Assets/Game/Core/Bootstrap/Loading/Operations/RemoteConfigFetchOperation.cs` | Loading-операция: фетч Remote Config |
| `Assets/Game/Core/Analytics/Providers/Firebase/FirebaseAnalyticsProvider.cs` | Имплементация IAnalyticsProvider |
| `Assets/Game/Features/EventOrchestration/Module/Impl/FirebaseClock.cs` | Синхронизация времени с сервером |
| `Assets/Game/Features/EventOrchestration/Module/Impl/FirebaseRemoteScheduleProvider.cs` | Загрузка расписания ивентов |
| `Assets/Game/Features/CardCollection/.../BaseFirebaseProvider.cs` | Базовый класс для чтения JSON из Remote Config |
| `Assets/Game/Features/CardCollection/.../FirebaseEventConfigProvider.cs` | Загрузка конфигурации ивентов из Remote Config |

---

## Последовательность инициализации (Bootstrap)

```
Phase 1 — Technical Init (Sequential)
  └─ FirebaseDependenciesOperation
       ├─ CheckUnityGamingServices()
       └─ FirebaseApp.CheckAndFixDependenciesAsync()

Phase 3 — Data Load (Parallel)
  └─ RemoteConfigFetchOperation
       └─ RemoteConfigLoader.FetchAndActivateAsync()
            └─ FetchAsync() → ActivateAsync()
```

---

## DI (VContainer)

- **`BootstrapInstaller`** — регистрирует `RemoteConfigLoader` как Singleton, подключает `FirebaseAnalyticsProvider`
- **`AnalyticsVContainerBindings`** — `FirebaseAnalyticsProvider` как `IAnalyticsProvider`
- **`CardCollectionImplInstaller`** — `FirebaseEventConfigProvider` как `IEventConfigProvider`

---

## Android/iOS зависимости

**Android Gradle** (`mainTemplate.gradle`):

```
firebase-app-unity:13.9.0
firebase-analytics-unity:13.9.0  →  firebase-analytics:23.0.0
firebase-auth-unity:13.9.0        →  firebase-auth:24.0.1
firebase-config-unity:13.9.0     →  firebase-config:23.0.1
firebase-database-unity:13.9.0   →  firebase-database:22.0.1
```

---

## Паттерны использования

### Remote Config
- Основной способ поставки конфигурации в игру
- Фетчится при старте (нулевой minimum fetch interval)
- `BaseFirebaseProvider<T,TK>` — базовый класс для всех провайдеров, читающих JSON из Remote Config
- Поставляет конфиги ивентов и live ops расписания

### Realtime Database
- Используется только для одного reference: `.info/serverTimeOffset`
- `FirebaseClock` читает offset и синхронизирует локальное время с серверным

### Analytics
- Включён по умолчанию (вместе с Debug provider)
- Поддерживает User ID и кастомные user properties
- Типы параметров: `string`, `int`, `long`, `float`/`double`, `bool` (→ `0L`/`1L`)
- Max: 100 событий в очереди, 40 символов в имени события, 50 параметров на событие
