# Assembly Definition Rules

Правила организации `.asmdef` файлов для Unity-проекта.
Документ описывает соглашения и примеры для проекта MyBookstore и предназначен для строгого соблюдения при создании новых модулей.

---

## 1. Зачем нужны asmdef

- **Инкрементальная компиляция** — изменение файла перекомпилирует только его сборку и зависимые от неё, а не весь проект.
- **Явные зависимости** — граф `references` — единственный контракт того, что может использовать модуль.
- **Изоляция слоёв** — `noEngineReferences: true` гарантирует отсутствие Unity API в доменном коде.
- **Изоляция тестов** — Editor-only сборки не попадают в билд.

---

## 2. Архитектурные слои и их правила

Каждая нетривиальная фича организуется в слои. Зависимости идут строго **вниз** — верхний слой знает о нижних, нижний о верхних — никогда.

```
┌─────────────────────────────────┐
│  Bootstrap / Composition Root   │  знает обо всём, связывает всё
├─────────────────────────────────┤
│  Feature Root (Facade)          │  объединяет все слои фичи
├─────────────────────────────────┤
│  Presentation                   │  UI, MonoBehaviour, VContainer EntryPoints
├─────────────────────────────────┤
│  Application                    │  use cases, сервисы, оркестрация
├─────────────────────────────────┤
│  Domain                         │  бизнес-логика, модели — NO Unity
├─────────────────────────────────┤
│  API / Abstractions             │  интерфейсы, DTOs — публичный контракт
├─────────────────────────────────┤
│  Infrastructure / Core          │  HTTP, Save, Config — общие утилиты
└─────────────────────────────────┘
```

### Правила направления зависимостей

| Слой | Может ссылаться на | Не может ссылаться на |
|---|---|---|
| Bootstrap | всё | — |
| Feature Facade | все слои фичи + инфраструктура + другие фичи (через API) | Bootstrap |
| Presentation | Application, Domain, API, UIShared, Infrastructure | другие фичи (напрямую) |
| Application | Domain, API, Infrastructure | Presentation, Bootstrap |
| Domain | ничего (или только API/Abstractions) | всё остальное |
| API/Abstractions | ничего | всё остальное |
| Infrastructure | Core.Models | фичи |

---

## 3. Слои подробно

### 3.1 Domain — чистая бизнес-логика

```json
{
    "name": "MyFeature.Domain",
    "noEngineReferences": true,
    "references": [],
    "autoReferenced": false
}
```

**Правила:**
- `noEngineReferences: true` — **обязательно**. Ни `UnityEngine`, ни `UnityEditor`.
- Нет ссылок на другие сборки (исключение — общие Value Objects из `Core.Models`).
- `autoReferenced: false` — только явные подписчики могут его использовать.
- Содержит: модели, агрегаты, domain events, value objects, domain service interfaces.

---

### 3.2 Application — use cases

```json
{
    "name": "MyFeature.Application",
    "references": ["MyFeature.Domain", "com.cysharp.unitask"],
    "autoReferenced": false
}
```

**Правила:**
- Ссылается только на Domain и утилиты (`UniTask`, `R3`).
- Не знает об UI, MonoBehaviour, VContainer.
- Содержит: use cases, application services, фасады, команды/запросы.

---

### 3.3 Presentation — UI и Unity-специфика

```json
{
    "name": "MyFeature.Presentation",
    "references": [
        "MyFeature.Domain",
        "MyFeature.Application",
        "UIShared",
        "UISystem",
        "VContainer",
        "VContainer.Unity",
        "com.cysharp.unitask"
    ],
    "autoReferenced": false
}
```

**Правила:**
- Содержит View, ViewModel, MonoBehaviour, VContainer EntryPoints.
- Не регистрирует DI-биндинги (это делает Facade или Bootstrap).
- Не обращается напрямую к сервисам других фич — только через интерфейсы.

---

### 3.4 Feature Facade (Root) — точка входа

```json
{
    "name": "MyFeature",
    "references": [
        "MyFeature.Domain",
        "MyFeature.Application",
        "MyFeature.Presentation",
        "Infrastructure",
        "VContainer",
        "VContainer.Unity",
        "com.cysharp.unitask"
    ],
    "autoReferenced": true
}
```

**Правила:**
- Единственная сборка фичи с `autoReferenced: true`.
- Содержит `*VContainerBindings.cs` — extension-методы на `IContainerBuilder`.
- Bootstrap и GameInstaller ссылаются только на Facade, не на внутренние слои.
- Может ссылаться на **API** (интерфейсы) других фич, но не на их Implementation.

---

### 3.5 API / Abstractions — публичный контракт

```json
{
    "name": "MyFeature.API",
    "references": ["com.cysharp.unitask"],
    "autoReferenced": false
}
```

**Правила:**
- Только интерфейсы и DTOs. Никаких классов с реализацией.
- Минимум внешних зависимостей (только `UniTask`, `R3`).
- Когда другая фича нужна — она ссылается только на `.API`, не на Facade.

**Пример:** `Inventory.API` содержит `IInventoryService`, `InventoryItemDelta`. Фича `Shop` ссылается только на `Inventory.API`, а не на `Inventory` (Facade).

---

### 3.6 Tests (Editor only)

```json
{
    "name": "MyFeature.Tests.Editor",
    "references": [
        "MyFeature",
        "MyFeature.Application",
        "MyFeature.Domain",
        "com.cysharp.unitask"
    ],
    "includePlatforms": ["Editor"],
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferenced": false
}
```

**Правила:**
- `includePlatforms: ["Editor"]` — **обязательно**.
- `overrideReferences: true` + `precompiledReferences: ["nunit.framework.dll"]` — **обязательно** для NUnit.
- `autoReferenced: false`.
- Называется `*.Tests.Editor` — всегда.
- Может ссылаться на все внутренние слои фичи (в отличие от внешнего кода).

---

## 4. Именование

### Паттерн: `{Namespace}.{Layer}`

```
Book.Sell.Domain
Book.Sell.Application
Book.Sell.Presentation
Book.Sell                    ← Facade, без суффикса
Book.Sell.API                ← если нужен публичный контракт
Book.Sell.Tests.Editor

Inventory.API
Inventory.Implementation     ← или Inventory (Facade)
Inventory.Tests.Editor

Game.Bootstrap               ← Composition Root
Game.Infrastructure          ← общие утилиты
Game.Core.Models             ← общие доменные типы
```

### Правила именования

| Правило | Пример |
|---|---|
| Фича без слоёв → просто название | `FortuneWheel`, `Analytics` |
| Фича с DDD-слоями → суффиксы Domain/Application/Presentation | `BattlePass.Domain` |
| Публичный контракт → `.API` или `.Abstractions` | `Inventory.API` |
| Тесты → `*.Tests.Editor` | `BattlePass.Tests.Editor` |
| Инфраструктура → `Infrastructure` или `Core.*` | `Core.Models` |
| Корневой биндер → `Game.Bootstrap` | |

### Имя файла = значение поля `name`

```
Assets/Game/Features/Inventory/Runtime/API/Inventory.API.asmdef      → name: "Inventory.API"
Assets/Game/Features/Inventory/Runtime/Implementation/Inventory.asmdef → name: "Inventory"
```

---

## 5. Поля asmdef — справочник

| Поле | Когда использовать |
|---|---|
| `noEngineReferences: true` | Domain слой — обязательно |
| `autoReferenced: true` | Только Facade или инфраструктурные сборки |
| `autoReferenced: false` | Все внутренние слои (Domain, Application, Presentation, API, Tests) |
| `includePlatforms: ["Editor"]` | Все тестовые сборки — обязательно |
| `overrideReferences: true` | Тестовые сборки с precompiled refs (nunit) |
| `defineConstraints` | Условная компиляция (напр. `"UNITY_LEVELPLAY"`) |
| `versionDefines` | Условная компиляция по наличию пакета |

---

## 6. Матрица зависимостей между фичами

Правило: фичи **не ссылаются друг на друга напрямую**. Связь — только через `.API` интерфейсы или через Bootstrap (DI).

```
                     Core  Infra Analytics  Save  Inventory.API  UIShared  Bootstrap
Game.Bootstrap         ✓    ✓      ✓         ✓        ✓             ✓       —
BookSell (Facade)      ✓    ✓      ✓         —        ✓             ✓       —
Shop (Facade)          ✓    ✓      —         —        ✓             ✓       —
Quest (Facade)         ✓    ✓      ✓         ✓        —             —       —
RewardDrop (Facade)    ✓    ✓      —         —        ✓             —       —
Inventory (Facade)     ✓    ✓      ✓         ✓        —             ✓       —
IAP (Facade)           ✓    ✓      ✓         —        —             —       —
```

**Нарушение паттерна:** `Shop` ссылается на `Inventory` (Facade) → ошибка. Правильно — `Shop` ссылается на `Inventory.API`.

---

## 7. Примеры для проекта MyBookstore

### Простая фича без DDD-слоёв (Analytics, IAP)

```
Assets/Game/Features/Analytics/
└── Analytics.asmdef               name: "Analytics"
    references: ["Infrastructure", "VContainer", "VContainer.Unity"]
    autoReferenced: true
```

### Фича средней сложности (Shop, Quest)

```
Assets/Game/Features/Shop/
├── Runtime/
│   ├── Shop.asmdef                name: "Shop"
│   │   references: ["Infrastructure", "Inventory.API", "UIShared", "UISystem", "VContainer"]
│   │   autoReferenced: true
│   └── API/
│       └── Shop.API.asmdef        name: "Shop.API"
│           references: ["com.cysharp.unitask"]
│           autoReferenced: false
└── Tests/Editor/
    └── Shop.Tests.Editor.asmdef   name: "Shop.Tests.Editor"
        includePlatforms: ["Editor"]
        references: ["Shop", "Shop.API", "Infrastructure", "Inventory.API", "com.cysharp.unitask"]
        overrideReferences: true
        precompiledReferences: ["nunit.framework.dll"]
        autoReferenced: false
```

### Сложная фича с DDD-слоями (BookSell — главная фича)

```
Assets/Game/Features/BookSell/
├── Runtime/
│   ├── Domain/
│   │   └── Book.Sell.Domain.asmdef        name: "Book.Sell.Domain"
│   │       references: []
│   │       noEngineReferences: true
│   │       autoReferenced: false
│   │
│   ├── Application/
│   │   └── Book.Sell.Application.asmdef   name: "Book.Sell.Application"
│   │       references: ["Book.Sell.Domain", "com.cysharp.unitask"]
│   │       autoReferenced: false
│   │
│   ├── Presentation/
│   │   └── Book.Sell.Presentation.asmdef  name: "Book.Sell.Presentation"
│   │       references: ["Book.Sell.Domain", "Book.Sell.Application",
│   │                    "UIShared", "UISystem", "VContainer", "VContainer.Unity"]
│   │       autoReferenced: false
│   │
│   └── Book.Sell.asmdef                   name: "Book.Sell"  ← Facade
│       references: ["Book.Sell.Domain", "Book.Sell.Application", "Book.Sell.Presentation",
│                    "Infrastructure", "Inventory.API", "VContainer", "VContainer.Unity"]
│       autoReferenced: true
│
└── Tests/Editor/
    └── Book.Sell.Tests.Editor.asmdef      name: "Book.Sell.Tests.Editor"
        includePlatforms: ["Editor"]
        overrideReferences: true
        precompiledReferences: ["nunit.framework.dll"]
        autoReferenced: false
```

### Общий контракт инвентаря (используется несколькими фичами)

```
Assets/Game/Features/Inventory/
├── Runtime/
│   ├── API/
│   │   └── Inventory.API.asmdef           name: "Inventory.API"
│   │       references: ["com.cysharp.unitask"]
│   │       autoReferenced: false
│   │
│   └── Inventory.asmdef                   name: "Inventory"  ← Facade + Implementation
│       references: ["Inventory.API", "Infrastructure", "Core.Models",
│                    "UIShared", "UISystem", "VContainer", "VContainer.Unity"]
│       autoReferenced: true
│
└── Tests/Editor/
    └── Inventory.Tests.Editor.asmdef
```

---

## 8. Типичные ошибки

### ❌ Циклическая зависимость
```
Shop → Inventory → Shop   // сломает компиляцию
```
**Решение:** Shop → Inventory.API. Связь устанавливается через DI в Bootstrap.

### ❌ Presentation ссылается на другую фичу напрямую
```
Book.Sell.Presentation → Shop  // нарушение изоляции
```
**Решение:** Инжектировать `IShopService` (из `Shop.API`) через DI.

### ❌ Domain знает о Unity
```
// В Book.Sell.Domain:
using UnityEngine;  // ошибка компиляции при noEngineReferences: true
```
`noEngineReferences: true` защищает от этого на уровне компилятора.

### ❌ Тестовая сборка без `includePlatforms: ["Editor"]`
Тесты попадут в runtime-билд. NUnit в продакшне — ошибка.

### ❌ `autoReferenced: true` на внутреннем слое
```
// Book.Sell.Domain.asmdef
"autoReferenced": true  // опасно
```
Любой код в проекте получит доступ к внутренностям Domain. Должно быть `false`.

---

## 9. Checklist при создании нового модуля

- [ ] Определить, нужны ли DDD-слои (Domain/Application/Presentation) или достаточно одной сборки
- [ ] Domain: `noEngineReferences: true`, `autoReferenced: false`, нет ссылок на Unity-пакеты
- [ ] Application: только Domain + UniTask/R3, никаких UI-зависимостей
- [ ] Facade: `autoReferenced: true`, содержит `*VContainerBindings.cs`
- [ ] Если другие фичи будут использовать этот модуль — создать `.API` сборку с интерфейсами
- [ ] Тесты: `includePlatforms: ["Editor"]`, `overrideReferences: true`, `nunit.framework.dll` в precompiled
- [ ] Имя сборки совпадает с именем `.asmdef` файла
- [ ] Нет ссылок на Bootstrap из фич
- [ ] Нет ссылок на Facade других фич — только на их `.API`
