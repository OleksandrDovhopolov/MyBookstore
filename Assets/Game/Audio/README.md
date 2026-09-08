# Audio Assets

This folder is the shared home for production audio files used by the game flow, UI, and reusable SFX.

## Layout

- `Music/` - long looping tracks: `home.mp3`, `sell_day_1.mp3`, `sell_day_2.mp3`.
- `Ui/` - short interface sounds such as button clicks, popups, and errors. Currently empty:
  `click_buttom.wav` and `pop_up.ogg` still live in `Sfx/`. Moving them is safe — `AudioCatalog`
  references assets by guid, not by path.
- `Sfx/` - gameplay sounds such as buys, object placement, pickups, and soft currency gains.

## Import Settings

Music clips should use:

- Load Type: `Streaming`
- Compression Format: `Vorbis`
- Preload Audio Data: off
- Load In Background: on

Short UI and SFX clips should use:

- Load Type: `Decompress On Load`
- Compression Format: `PCM` or `ADPCM`

Runtime code reads clips through `AudioCatalog`, which lives next to its script at
`Assets/Game/Infrastructure/Audio/AudioCatalog.asset` and is assigned on `BootstrapInstaller.asset`
(field `_audioCatalog`). Leaving it unassigned is not an error — audio just stays a no-op.
