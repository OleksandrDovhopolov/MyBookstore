# Audio Assets

This folder is the shared home for production audio files used by the game flow, UI, and reusable SFX.

## Layout

- `Music/` - long looping tracks such as `home1.mp3`, `sell_day_1.mp3`, `sell_day_2.mp3`.
- `Ui/` - short interface sounds such as button clicks, popups, bubbles, and errors.
- `Sfx/` - gameplay sounds such as buys, object placement, pickups, and soft currency gains.

## Import Settings

Music clips should use:

- Load Type: `Streaming`
- Compression Format: `Vorbis`
- Preload Audio Data: off

Short UI and SFX clips should use:

- Load Type: `Decompress On Load`
- Compression Format: `PCM` or `ADPCM`

Runtime code reads clips through `AudioCatalog`. Create it via `Create > Game > Audio > Audio Catalog`,
place the asset in this folder, then assign it on `BootstrapInstaller.asset`.
