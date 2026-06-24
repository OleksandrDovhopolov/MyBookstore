# TODO

Рабочий список задач. Разбит по категориям: **Геймплей**, **Инфраструктура**, **Визуал**.
Новые задачи добавляются в конец соответствующей категории.

Статусы: `[ ]` — todo, `[~]` — в работе, `[x]` — готово.

---

## 🎮 Геймплей

_(пока нет задач)_

---

## 🛠️ Инфраструктура

- [ ] **INF-1. Загрузка спрайтов жанровых книг из Addressables + gating бутстрапа.**
  Сейчас `GameplaySceneView` ([GameplaySceneView.cs](../Assets/Game/UI/GameplayScene/GameplaySceneView.cs))
  держит спрайты жанров (`_classicGenreSprite` … `_fantasyGenreSprite`) как serialized-поля и резолвит
  их в `GetGenreSprite(BookGenre)`. Нужно грузить их из Addressables (через `IUiSpriteProvider` /
  `ProdAddressablesWrapper`) для `_genreBookCountPool`, по аналогии с newspaper/rewards.
  `MainSceneBootstrap` ([MainSceneBootstrap.cs](../Assets/Game/UI/GameplayScene/MainSceneBootstrap.cs))
  должен дождаться загрузки этих спрайтов перед показом контента:
  `await UniTask.WaitUntil(() => IsWindowShown && spritesLoaded)` — проверять **оба** условия
  (окно показано И спрайты загружены).

- [ ] **INF-2. Подключить DoTween** (импорт пакета + asmdef-ссылки + базовая обёртка/хелперы под анимации).

---

## 🎨 Визуал

- [ ] **VIS-1. Анимация «полёта» золота из HUD к кнопке** (в newspaper-окне):
  золото вылетает из HUD-счётчика и летит к кнопке покупки. Зависит от **INF-2 (DoTween)**.

- [ ] **VIS-2. Свёрстать окно Preparation.** Добавить в окно полку с инвентарём в разрезе жанров.
  Окно: `PreparationWindow` (`Assets/Game/Features/Preparation/UI/`).

- [ ] **VIS-3. Свёрстать окно декора** (decor window).
