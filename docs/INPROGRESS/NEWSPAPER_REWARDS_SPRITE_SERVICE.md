# Newspaper / Rewards Sprite Service

**Project**: MyBookstore  
**Date**: 2026-06-23

---

## Context

Сейчас логика спрайтов для newspaper UI живёт внутри оконных view/controller классов.

Из-за этого у `NewspaperWindow` спрайты грузятся при открытии окна, а при закрытии освобождаются.  
Следствие: при следующем открытии окна загрузка повторяется и иконки появляются с задержкой.

Целевое направление: вынести эту ответственность в отдельный долгоживущий сервис, который:
- загружает нужные спрайты один раз;
- хранит их в памяти между открытиями окон;
- отдаёт уже готовые `Sprite` мгновенно;
- сам управляет временем жизни и очисткой кэша.

---

## Why Current Logic Delays

Текущая задержка появляется не из-за `await` сама по себе, а из-за жизненного цикла ресурсов:
- окно открывается;
- view запускает загрузку спрайтов;
- окно закрывается;
- view освобождает загруженные спрайты;
- при следующем открытии всё повторяется заново.

Чтобы сделать показ мгновенным, кэш должен жить вне окна.

---

## Intended Service Shape

Будущий сервис может быть оформлен как singleton наподобие `INewspaperSpriteProvider` или более общего `IUiSpriteProvider`.

Минимальные обязанности сервиса:
- preload нужных newspaper/rewards спрайтов;
- sync-access к уже загруженным `Sprite`;
- async lazy-load на первый запрос;
- внутренний cache по ключу/address/id;
- явный `ReleaseAll` или scoped cleanup в контролируемой точке приложения, а не в `OnDispose()` окна.

Для `NewspaperWindow` сервис должен уметь:
- вернуть общий спрайт для book offers;
- вернуть decor-спрайт по lot id:
  - `newspaper_decor_vintage_globe`
  - `newspaper_decor_coffee_pot`

Для `RewardsWindow` сервис должен уметь:
- вернуть иконку reward/genre по `resourceId`;
- вернуть fallback-иконку для non-book reward.

---

## Current Integration Points

### Newspaper window

- [NewspaperWindow](/Users/oleksandrdovhopolov/Unity/MyBookshop/MyBookstore/Assets/Game/Features/Newspaper/UI/NewspaperWindow.cs:38)  
  Открывает окно, запускает фоновую подгрузку и затем обновляет видимые иконки.

- [NewspaperWindow](/Users/oleksandrdovhopolov/Unity/MyBookshop/MyBookstore/Assets/Game/Features/Newspaper/UI/NewspaperWindow.cs:88)  
  `LoadSpritesAndRefreshAsync` ждёт завершения загрузки и затем обновляет только активные карточки.

- [NewspaperWindowView](/Users/oleksandrdovhopolov/Unity/MyBookshop/MyBookstore/Assets/Game/Features/Newspaper/UI/NewspaperWindowView.cs:18)  
  Хранит временные address-поля для newspaper спрайтов.

- [NewspaperWindowView](/Users/oleksandrdovhopolov/Unity/MyBookshop/MyBookstore/Assets/Game/Features/Newspaper/UI/NewspaperWindowView.cs:32)  
  `PreloadSpritesAsync` грузит спрайты через `ProdAddressablesWrapper`.

- [NewspaperWindowView](/Users/oleksandrdovhopolov/Unity/MyBookshop/MyBookstore/Assets/Game/Features/Newspaper/UI/NewspaperWindowView.cs:65)  
  `ReleaseLoadedSprites` освобождает спрайты при закрытии окна.

### Rewards window

- [RewardsWindow](/Users/oleksandrdovhopolov/Unity/MyBookshop/MyBookstore/Assets/Game/Features/Newspaper/UI/RewardsWindow.cs:34)  
  Передаёт в builder resolver `View.GetIconForReward`.

- [RewardWindowView](/Users/oleksandrdovhopolov/Unity/MyBookshop/MyBookstore/Assets/Game/Features/Newspaper/UI/Rewards/RewardWindowView.cs:25)  
  Хранит текущие reward/genre/fallback спрайты прямо во view через serialized fields.

- [RewardWindowView](/Users/oleksandrdovhopolov/Unity/MyBookshop/MyBookstore/Assets/Game/Features/Newspaper/UI/Rewards/RewardWindowView.cs:70)  
  `GetIconForReward` содержит текущий локальный маппинг `resourceId -> Sprite`.

---

## Migration Note

При переносе в сервис окна не должны:
- сами грузить спрайты;
- сами кэшировать спрайты;
- сами вызывать `Release` на каждый `OnDispose()`.

Окна должны только запрашивать уже готовую иконку у сервиса и обновлять UI.
