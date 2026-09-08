# Аудио-система (MyBookstore) — архитектура сервиса

> **Слой:** инфраструктура (`Assets/Game/Infrastructure/Audio/`, asmdef `Infrastructure`)
> **Движок:** встроенный Unity `AudioSource` (без FMOD)
> **DI:** VContainer (`GlobalLifetimeScope` / `BootstrapInstaller`)
> **Связанная задача:** INF-3 / REL-8

Намеренно лёгкая обёртка под cozy-sim: UI-клики, короткие SFX, ambient-подложка и спокойная
музыка. FMOD сознательно не используется; при росте требований первым шагом будет Unity
`AudioMixer`, а не middleware.

---

## 1. Структура

```text
Assets/Game/Infrastructure/Audio/
├── IAudioService.cs                  # Публичный контракт
├── AudioService.cs                   # Реализация (4 AudioSource), IDisposable
├── AudioRoot.cs                      # DontDestroyOnLoad-корень с источниками
├── AudioCatalog.cs                   # ScriptableObject с дефолтными клипами и музыкой
├── AudioChannelId.cs                 # enum шин: Master/Music/Sfx/Ui/Ambient
├── AudioVolumeSettings.cs            # [Serializable] громкости по шинам
├── IAudioSettingsStore.cs            # Контракт сохранения настроек
├── PlayerPrefsAudioSettingsStore.cs  # Реализация на PlayerPrefs
├── IAudioClipLoader.cs               # Контракт загрузки клипа по адресу
├── AddressablesAudioClipLoader.cs    # Реализация поверх ProdAddressablesWrapper
├── Audio.cs                          # Статический фасад для MonoBehaviour без DI
└── Tests/Editor/AudioServiceTests.cs # EditMode-тесты

Assets/Game/Core/Installers/Features/Audio/
├── MusicDirector.cs                  # Переключает музыку хаб/день
└── MusicDirectorVContainerBindings.cs

Assets/Game/Infrastructure/UIShared/Audio/
├── UiButtonClickAudio.cs             # Shared UI-компонент клика, asmdef Infrastructure
└── WindowAudio.cs                    # Shared UI-компонент open/close, asmdef Infrastructure
```

Регистрация сервиса — в `InfrastructureVContainerBindings.RegisterInfrastructure(audioCatalog)`.
Музыкальный дирижёр регистрируется отдельно через `RegisterMusicDirector()`.

---

## 2. Контракт — `IAudioService`

```csharp
AudioVolumeSettings Volumes { get; }                       // копия, не живой объект
void SetVolume(AudioChannelId channel, float volume);       // клампится 0..1, сохраняется
float GetVolume(AudioChannelId channel);

void PlayMusic(AudioClip clip, bool loop = true, bool restartIfSame = false);
UniTask PlayMusicFadedAsync(AudioClip clip, float fadeSeconds, CancellationToken ct, bool loop = true);
void PlaySfx(AudioClip clip, float volumeScale = 1f);
void PlaySfxAt(AudioClip clip, Vector3 position, float volumeScale = 1f);
void PlayUi(AudioClip clip, float volumeScale = 1f);
void PlayAmbient(AudioClip clip, bool loop = true, bool restartIfSame = false);

UniTask PlayMusicAsync(string address, CancellationToken ct, bool loop = true, bool restartIfSame = false);
UniTask PlaySfxAsync(string address, CancellationToken ct, float volumeScale = 1f);
UniTask PlayUiAsync(string address, CancellationToken ct, float volumeScale = 1f);
UniTask PlayAmbientAsync(string address, CancellationToken ct, bool loop = true, bool restartIfSame = false);

void StopMusic();  void StopSfx();  void StopAmbient();  void StopAll();
bool IsMusicPlaying { get; }
void ReleaseCachedClips();
void SetMuted(bool muted);
```

Sync-методы принимают готовый `AudioClip` и игнорируют `null`. Async-методы грузят клип по
строковому адресу через Addressables и затем делегируют в sync-метод. `PlayMusicFadedAsync`
используется для переключения треков хаб/локация: fade-out, замена клипа, fade-in на одном
`MusicSource`.

---

## 3. Шины и модель громкости

`AudioChannelId`: `Master`, `Music`, `Sfx`, `Ui`, `Ambient`.

`AudioRoot` создаётся лениво при первом реальном проигрывании, `DontDestroyOnLoad` в play-mode,
и держит четыре `AudioSource`: `Music` (loop), `Sfx`, `Ui`, `Ambient` (loop).

Итоговая громкость канала: `ChannelVolume(ch) = muted ? 0 : Master * Volume(ch)`.

- **Music** — `MusicSource.volume = ChannelVolume(Music) * _musicFadeScale`. Множитель нужен,
  чтобы настройка Music, изменённая посреди фейда, не затиралась следующим кадром перехода.
- **Ambient** — `AmbientSource.volume = ChannelVolume(Ambient)`, обновляется живо.
- **Sfx / Ui** — one-shot'ы. `ApplyVolumes()` держит их `source.volume = 1f`, а затухание идёт
  один раз через `PlayOneShot(clip, ScaledVolume(ch, volumeScale))`.

Настройки громкости сохраняются в `PlayerPrefs` через `IAudioSettingsStore` при каждом
`SetVolume` (ключи `audio.master/music/sfx/ui/ambient`).

---

## 4. Где лежат аудио-ассеты

```text
Assets/Game/Audio/
├── README.md
├── Music/   # home.mp3, sell_day_1.mp3, sell_day_2.mp3
├── Ui/      # пока пусто
└── Sfx/     # click_buttom.wav, pop_up.ogg, Buy.wav, error_sound.ogg, notification.ogg,
             # put_object.ogg, take_object.ogg, sell_click.ogg, soft_currency_gained.ogg,
             # reward_main.ogg, jingle_new_discovery.ogg, tap_dialog.ogg, chest_item.ogg
```

Разделение `Ui/` и `Sfx/` пока не соблюдается: клики и попапы лежат в `Sfx/`. На код это не влияет
(`AudioCatalog` ссылается на ассеты по guid), но при следующем пополнении звуки стоит разложить.

Import settings:

- музыка: `Load Type: Streaming`, `Compression Format: Vorbis`, `Preload Audio Data: off`,
  `Load In Background: on`;
- короткие UI/SFX: `Load Type: Decompress On Load`, `Compression Format: PCM` или `ADPCM`.

`AudioCatalog.asset` лежит рядом со своим скриптом — `Assets/Game/Infrastructure/Audio/AudioCatalog.asset` —
и назначается на `BootstrapInstaller.asset` (поле `_audioCatalog`). Код обязан работать и при
неназначенном каталоге: тогда `RegisterInfrastructure` пишет предупреждение, а вся аудио-система
остаётся no-op.

---

## 5. Каталог и фолбэки

`AudioCatalog` содержит прямые ссылки `AudioClip`, без Addressables-группы. Это общий runtime-реестр
повторяемых звуков, которые вызываются из кода и назначаются вручную в `AudioCatalog.asset`.

- UI defaults: `ButtonClick`, `WindowOpen`, `WindowClose`;
- gameplay SFX: покупки, ошибки, декор, продажи, уведомления, награды, диалог;
- музыка: `HubMusic`, массив `SalesDayMusic`, `MusicFadeSeconds`;
- `GetSalesDayMusic(day)` выбирает `(day - 1) % SalesDayMusic.Length`; пустой массив возвращает `null`.

Статический фасад `Audio` хранит привязанный каталог (`Audio.Catalog`) рядом с `IAudioService`.
`UiButtonClickAudio` и `WindowAudio` сначала используют клип из своего serialized-поля, а если оно
пустое, берут дефолт из каталога. `DecorPlacementWindow` работает так же для place/remove: serialized
поле на view остаётся override, каталог даёт общий fallback.

Это не вводит глобальный `SoundId`: каталог задаёт дефолты для повторяющихся событий, а особые звуки
остаются ссылкой на ассет в месте воспроизведения.

Правило канала: окна/кнопки/диалоговые реплики используют `Audio.PlayUi`; игровые события
(покупка, продажа, декор, валюта, разблокировка, итоги дня) используют `Audio.PlaySfx`.
Оба канала выключаются тумблером Sound.

| Поле каталога | Событие | Код |
|---|---|---|
| `ButtonClick` | дефолт клика по UI-кнопке | `UiButtonClickAudio` |
| `WindowOpen` | дефолт открытия окна | `WindowAudio` |
| `WindowClose` | дефолт закрытия окна | `WindowAudio` |
| `PurchaseSuccess` | успешная покупка лота | `ShopWindow.TryBuyAsync` |
| `ActionBlocked` | неуспешная покупка или unlock | `ShopWindow.TryBuyAsync`, `LocationWindow.UnlockAsync` |
| `DecorPlace` | декор поставлен или заменён | `DecorPlacementWindow.ApplyAsync` |
| `DecorRemove` | декор снят | `DecorPlacementWindow.RemoveAsync` |
| `CurrencyGained` | старт count-up золота после итогов дня | `ResourceCounterHudPresenter.AnimateCountUpInternalAsync` |
| `BookSold` | пассивная продажа или excellent-рекомендация | `SalesScreenView`, `RecommendationMinigameWindow.OnResolved` |
| `NewJournalEntry` | бейдж журнала перешёл `false -> true` | `HudMenuButtonsView.SetJournalBadge` |
| `RewardReceived` | popup наград показал непустую награду | `RewardsWindow.ApplyArgsToView` |
| `LocationDiscovered` | игрок успешно разблокировал локацию | `LocationWindow.UnlockAsync` |
| `DialogueLine` | появилась новая реплика | `DialogLineView.RevealAsync` |
| `DayCompletionItem` | итоги дня показали reward за завершение | `ResultsWindow.OnSummaryReady` |

Без сервиса, без каталога или без клипа все вызовы остаются no-op.

---

## 6. Музыка

`MusicDirector` живёт в `Game.Bootstrap`, потому что ему одновременно нужны:

- `IAudioService` из `Infrastructure`;
- `IGameFlowService` из `Game.Bootstrap.Loading`;
- `IDayProgressService` из `DayCycle`.

Паттерн жизненного цикла: `IStartable + IDisposable`. На `Start()` дирижёр подписывается на
`IGameFlowService.LocationLoadedChanged` и `IDayProgressService.PhaseChanged`, затем применяет
текущее состояние. На `Dispose()` отписывается и отменяет свой CTS.

Правило выбора:

```csharp
clip = gameFlow.IsLocationLoaded
    ? catalog.GetSalesDayMusic(dayProgress.Current.CurrentDay)
    : catalog.HubMusic;
```

Повторный запрос того же клипа отбрасывается guard'ом по `_lastRequested`, потому что события
могут приходить повторно: `LocationLoadedChanged` стреляет при ошибочном входе `true -> false`,
а `PhaseChanged` может прийти даже без фактической смены фазы. Переключение происходит под
шторкой перехода (`PlayCoverAsync` до `PlayRevealAsync`), поэтому fade-out/swap/fade-in успевает
спрятаться за экранным переходом.

---

## 7. Загрузка по адресу и кеш

`IAudioClipLoader` -> `AddressablesAudioClipLoader` оборачивает `ProdAddressablesWrapper`
(ref-counted). `AudioService.LoadClipAsync()` работает со счётчиком ссылок:

- проверка `_clipCache` до `LoadAsync`;
- при гонке повторной загрузки одного адреса лишняя ссылка освобождается через `Release`;
- linked-CTS с `_disposeCts`: при диспозе/отмене после `await` загруженный клип освобождается;
- `ReleaseCachedClips()` снимает все кешированные клипы; вызывать после Stop, не во время игры.

---

## 8. Настройки

В UI остаются два тумблера, но они покрывают четыре шины:

| Тумблер | Пишет | Читает enabled, если хоть одна > 0 |
|---|---|---|
| Sound | `Sfx`, `Ui` | `Sfx` или `Ui` |
| Music | `Music`, `Ambient` | `Music` или `Ambient` |

Единственная точка семантики — `SettingsAudioToggleAdapter`. `SettingsWindowController`,
`SettingsWindowView`, `UISwitch` и префаб окна не знают о составе шин.

---

## 9. Жизненный цикл и DI

```csharp
builder.RegisterInfrastructure(audioCatalog);
builder.RegisterDayCycleServices();
builder.RegisterMusicDirector();
```

`RegisterInfrastructure` биндит:

```csharp
builder.Register<IAudioSettingsStore, PlayerPrefsAudioSettingsStore>(Lifetime.Singleton);
builder.Register<IAudioClipLoader, AddressablesAudioClipLoader>(Lifetime.Singleton);
builder.Register<IAudioService, AudioService>(Lifetime.Singleton);
if (audioCatalog != null) builder.RegisterInstance(audioCatalog);

builder.RegisterBuildCallback(resolver =>
{
    Audio.Bind(resolver.Resolve<IAudioService>());
    Audio.BindCatalog(audioCatalog);
});
```

`AudioService` — `Singleton`, `IDisposable`. На диспозе: `_disposed=true`, отмена `_disposeCts`
и активного music fade, `Audio.Clear(this)`, `StopAll()`, уничтожение `AudioRoot`, затем
`ReleaseCachedClips()`.

---

## 10. Тесты

- `Assets/Game/Infrastructure/Audio/Tests/Editor/AudioServiceTests.cs` — расчёт громкости,
  mute, кеш загрузки и fade-scale музыки.
- `Assets/Game/Core/Installers/Tests/Editor/MusicDirectorTests.cs` — выбор hub/day музыки,
  wrap по дням, guard повторных событий, dispose.
- `Assets/Game/UI/GameplayScene/Tests/Editor/SettingsWindowTests.cs` — семантика Sound/Music
  тумблеров поверх шин.

---

## 11. Что ещё не сделано / следующий шаг

Инфраструктура и кодовые SFX-хуки закрыты: пустой каталог и отсутствие клипов оставляют игру без
звука и без исключений. Дальше остаётся ручная редакторская работа:

1. назначить все клипы в `AudioCatalog.asset`;
2. навесить `UiButtonClickAudio` на shared-кнопки, где компонента ещё нет;
3. навесить `WindowAudio` на префабы окон: сейчас open/close fallback есть в коде, но компонент
   должен присутствовать на view root окна.
