# Аудио-система (MyBookstore) — архитектура сервиса

> **Слой:** Инфраструктурный (`Assets/Game/Infrastructure/Audio/`, asmdef `Infrastructure`)
> **Движок:** встроенный Unity `AudioSource` (без FMOD)
> **DI:** VContainer (`GlobalLifetimeScope` / `BootstrapInstaller`)
> **Связанная задача:** INF-3 (`docs/TODO.md`)

Намеренно лёгкая обёртка под cozy-sim: пара UI-кликов, ambient-подложка, спокойная
музыка. FMOD сознательно не используется — у проекта нет выделенного саунд-дизайнера,
а вся ценность middleware (события, параметры, адаптивная музыка) без него не окупается.
При росте требований к микшированию первым шагом будет встроенный Unity `AudioMixer`,
а не FMOD.

---

## 1. Структура

```
Assets/Game/Infrastructure/Audio/
├── IAudioService.cs                  # Публичный контракт
├── AudioService.cs                   # Реализация (4 AudioSource), IDisposable
├── AudioRoot.cs                      # DontDestroyOnLoad-корень с источниками
├── AudioChannelId.cs                 # enum шин: Master/Music/Sfx/Ui/Ambient
├── AudioVolumeSettings.cs            # [Serializable] громкости по шинам
├── IAudioSettingsStore.cs           # Контракт сохранения настроек
├── PlayerPrefsAudioSettingsStore.cs  # Реализация на PlayerPrefs
├── IAudioClipLoader.cs              # Контракт загрузки клипа по адресу
├── AddressablesAudioClipLoader.cs   # Реализация поверх ProdAddressablesWrapper
├── Audio.cs                         # Статический фасад для MonoBehaviour без DI
└── Tests/Editor/AudioServiceTests.cs # EditMode-тесты (asmdef Infrastructure.Audio.Tests.Editor)
```

Регистрация — в `InfrastructureVContainerBindings.RegisterInfrastructure()`.

UI-потребители (паттерн «компонент + сериализованное поле»):
`Assets/Game/Core/UI/Audio/UiButtonClickAudio.cs`, `WindowAudio.cs` (asmdef `Game.Core.UI`).

---

## 2. Контракт — `IAudioService`

```csharp
AudioVolumeSettings Volumes { get; }              // копия (Clone), не живой объект
void  SetVolume(AudioChannelId channel, float volume);  // клампится 0..1, сохраняется
float GetVolume(AudioChannelId channel);

// Прямой AudioClip
void  PlayMusic(AudioClip clip, bool loop = true, bool restartIfSame = false);
void  PlaySfx(AudioClip clip, float volumeScale = 1f);
void  PlaySfxAt(AudioClip clip, Vector3 position, float volumeScale = 1f);
void  PlayUi(AudioClip clip, float volumeScale = 1f);
void  PlayAmbient(AudioClip clip, bool loop = true, bool restartIfSame = false);

// Загрузка по адресу Addressables (string) + проигрывание
UniTask PlayMusicAsync(string address, CancellationToken ct, bool loop = true, bool restartIfSame = false);
UniTask PlaySfxAsync(string address, CancellationToken ct, float volumeScale = 1f);
UniTask PlayUiAsync(string address, CancellationToken ct, float volumeScale = 1f);
UniTask PlayAmbientAsync(string address, CancellationToken ct, bool loop = true, bool restartIfSame = false);

void  StopMusic();  void StopSfx();  void StopAmbient();  void StopAll();
bool  IsMusicPlaying { get; }
void  ReleaseCachedClips();   // освобождает Addressables-клипы; звать после Stop, не во время игры
void  SetMuted(bool muted);
```

Sync-методы принимают готовый `AudioClip` и игнорируют `null`. Async-методы грузят клип
по строковому адресу (как `UiSpriteProvider` для спрайтов) и затем делегируют в sync-метод.

---

## 3. Шины и модель громкости

`AudioChannelId`: `Master`, `Music`, `Sfx`, `Ui`, `Ambient`.

`AudioRoot` создаётся **лениво** при первом проигрывании, `DontDestroyOnLoad`
(в play-mode), и держит четыре `AudioSource`: `Music` (loop), `Sfx`, `Ui`, `Ambient` (loop).

Итоговая громкость канала: `ChannelVolume(ch) = muted ? 0 : Master * Volume(ch)`.

- **Music / Ambient** — зацикленные подложки через `source.Play()`;
  `source.volume = ChannelVolume(ch)`, обновляется живо в `ApplyVolumes()`.
- **Sfx / Ui** — one-shot'ы. `ApplyVolumes()` держит их `source.volume = 1f`, а всё
  затухание идёт **один раз** через `PlayOneShot(clip, ScaledVolume(ch, volumeScale))`.
  Так устранено двойное затухание (раньше канал учитывался и в `source.volume`, и в
  `PlayOneShot` → `ChannelVolume²`).

Настройки громкости сохраняются в `PlayerPrefs` через `IAudioSettingsStore` при каждом
`SetVolume` (ключи `audio.master/music/sfx/ui/ambient`).

---

## 4. Загрузка по адресу и кеш

`IAudioClipLoader` → `AddressablesAudioClipLoader` оборачивает `ProdAddressablesWrapper`
(ref-counted). `AudioService.LoadClipAsync()` аккуратно работает со счётчиком ссылок:

- проверка `_clipCache` **до** `LoadAsync` (повторный `Play*Async` одного адреса не плодит ref);
- при гонке (пока шёл `await`, кто-то закешировал тот же адрес) — `Release` лишней ссылки;
- linked-CTS с `_disposeCts`: при диспозе/отмене после `await` загруженный клип
  освобождается, root не воскрешается;
- `ReleaseCachedClips()` снимает все кешированные клипы; `Dispose()` сначала `StopAll()`,
  потом релизит (нельзя выгружать играющий клип).

---

## 5. Жизненный цикл и DI

```csharp
builder.Register<IAudioSettingsStore, PlayerPrefsAudioSettingsStore>(Lifetime.Singleton);
builder.Register<IAudioClipLoader, AddressablesAudioClipLoader>(Lifetime.Singleton);
builder.Register<IAudioService, AudioService>(Lifetime.Singleton);

builder.RegisterBuildCallback(resolver =>
{
    resolver.Resolve<ILogService>();
    Audio.Bind(resolver.Resolve<IAudioService>());   // привязка статического фасада
});
```

`AudioService` — `Singleton`, `IDisposable`. На диспозе: `_disposed=true`, отмена `_disposeCts`,
`Audio.Clear(this)`, `StopAll()`, уничтожение `AudioRoot` (Destroy/DestroyImmediate по режиму),
`ReleaseCachedClips()`.

---

## 6. Паттерны использования

### Доменный сервис — конструкторная инъекция (предпочтительно)
```csharp
public SomeService(IAudioService audio) => _audio = audio;
```

### MonoBehaviour без DI — статический фасад `Audio`
```csharp
Audio.PlayUi(clip);
await Audio.PlayMusicAsync("mus_main_theme", ct);
Audio.SetVolume(AudioChannelId.Ambient, 0.5f);
```
Все `Audio.*` безопасны до инициализации: без привязки — no-op, `GetVolume` → `1f`,
async-методы возвращают `UniTask.CompletedTask`.

### UI-компоненты (паттерн «компонент + сериализованное поле»)
Идентичность звука — ссылка на ассет, привязанная **к месту, где он играет**, а не глобальный
enum звуков (см. историю решения и разбор референсной FMOD-системы).

- `UiButtonClickAudio` — `[RequireComponent(Button)]`, поле `AudioClip _click`, играет
  `Audio.PlayUi` на `onClick`.
- `WindowAudio` — поля `_open`/`_close`, играет на `OnEnable`/`OnDisable` (UIManager
  активирует/деактивирует view окна на show/hide).

Глобальные звуки, дёргаемые из кода во многих местах, — через каталог по строковому ключу
+ `Play*Async(address)`. Маленький scoped-enum под фиксированный набор фаз одной фичи —
допустим; глобального `SoundId` не заводим.

---

## 7. Тесты

`Assets/Game/Infrastructure/Audio/Tests/Editor/AudioServiceTests.cs`
(asmdef `Infrastructure.Audio.Tests.Editor`) — EditMode-тесты сервиса (расчёт громкости,
mute, сохранение настроек, кеш/ref-count загрузки).

---

## 8. Что ещё не сделано / на будущее

1. **Потребители в продакшене.** Компоненты `UiButtonClickAudio`/`WindowAudio` созданы, но
   звуки в префабах/окнах ещё не назначены; UI-слайдеры громкости в настройках не подключены.
2. **Save (INF-6).** Громкости в `PlayerPrefs`; после переноса Save в инфраслой реализацию
   `IAudioSettingsStore` можно подменить без правок сервиса.
3. **AudioMixer (опционально).** Для ducking/снапшотов/эффектов — встроенный Unity
   `AudioMixerGroup` на источники `AudioRoot`, по необходимости.
