using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Infrastructure.Audio
{
    public sealed class AudioService : IAudioService, IDisposable
    {
        private readonly IAudioSettingsStore _settingsStore;
        private readonly IAudioClipLoader _clipLoader;
        private readonly AudioVolumeSettings _volumes;
        private readonly Dictionary<string, AudioClip> _clipCache = new(StringComparer.Ordinal);
        private readonly object _clipCacheLock = new();
        private readonly CancellationTokenSource _disposeCts = new();

        private AudioRoot _root;
        private bool _muted;
        private bool _disposed;
        private float _musicFadeScale = 1f;
        private CancellationTokenSource _musicFadeCts;

        public AudioService(IAudioSettingsStore settingsStore, IAudioClipLoader clipLoader)
        {
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
            _clipLoader = clipLoader ?? throw new ArgumentNullException(nameof(clipLoader));
            _volumes = (_settingsStore.Load() ?? new AudioVolumeSettings()).Clone();
            ClampVolumes();
        }

        public AudioVolumeSettings Volumes => _volumes.Clone();

        public bool IsMusicPlaying => _root != null && _root.MusicSource != null && _root.MusicSource.isPlaying;

        public void SetVolume(AudioChannelId channel, float volume)
        {
            if (_disposed) return;

            var clamped = Mathf.Clamp01(volume);
            switch (channel)
            {
                case AudioChannelId.Master:
                    _volumes.Master = clamped;
                    break;
                case AudioChannelId.Music:
                    _volumes.Music = clamped;
                    break;
                case AudioChannelId.Sfx:
                    _volumes.Sfx = clamped;
                    break;
                case AudioChannelId.Ui:
                    _volumes.Ui = clamped;
                    break;
                case AudioChannelId.Ambient:
                    _volumes.Ambient = clamped;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(channel), channel, null);
            }

            ApplyVolumes();
            _settingsStore.Save(_volumes);
        }

        public float GetVolume(AudioChannelId channel)
        {
            return channel switch
            {
                AudioChannelId.Master => _volumes.Master,
                AudioChannelId.Music => _volumes.Music,
                AudioChannelId.Sfx => _volumes.Sfx,
                AudioChannelId.Ui => _volumes.Ui,
                AudioChannelId.Ambient => _volumes.Ambient,
                _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, null)
            };
        }

        public void PlayMusic(AudioClip clip, bool loop = true, bool restartIfSame = false)
        {
            if (_disposed) return;

            CancelMusicFade();
            _musicFadeScale = 1f;
            if (clip == null) return;

            var source = Root()?.MusicSource;
            if (source == null) return;
            if (!restartIfSame && source.clip == clip && source.isPlaying) return;

            source.clip = clip;
            source.loop = loop;
            source.volume = ChannelVolume(AudioChannelId.Music) * _musicFadeScale;
            source.Play();
        }

        public async UniTask PlayMusicFadedAsync(
            AudioClip clip, float fadeSeconds, CancellationToken ct, bool loop = true)
        {
            if (_disposed) return;
            if (ct.IsCancellationRequested) return;

            if (fadeSeconds <= 0f)
            {
                if (clip != null)
                    PlayMusic(clip, loop);
                else
                    StopMusic();
                return;
            }

            var source = clip == null ? _root?.MusicSource : Root()?.MusicSource;
            if (source == null) return;
            if (clip != null && source.clip == clip && source.isPlaying) return;

            var fadeCts = StartMusicFade(ct);
            var token = fadeCts.Token;

            try
            {
                if (source.isPlaying && _musicFadeScale > 0f)
                    await FadeMusicToAsync(0f, fadeSeconds, token);
                else
                    SetMusicFadeScale(0f);

                if (clip == null)
                {
                    StopMusicInternal();
                    SetMusicFadeScale(1f);
                    return;
                }

                token.ThrowIfCancellationRequested();
                source.clip = clip;
                source.loop = loop;
                source.Play();
                await FadeMusicToAsync(1f, fadeSeconds, token);
            }
            catch (OperationCanceledException) when (_disposed || token.IsCancellationRequested)
            {
            }
            finally
            {
                if (ReferenceEquals(_musicFadeCts, fadeCts))
                    _musicFadeCts = null;

                fadeCts.Dispose();
            }
        }

        public async UniTask PlayMusicAsync(
            string address, CancellationToken ct, bool loop = true, bool restartIfSame = false)
        {
            var clip = await LoadClipAsync(address, ct);
            if (_disposed || ct.IsCancellationRequested || clip == null) return;
            PlayMusic(clip, loop, restartIfSame);
        }

        public void StopMusic()
        {
            CancelMusicFade();
            _musicFadeScale = 1f;
            StopMusicInternal();
        }

        private void StopMusicInternal()
        {
            if (_root == null || _root.MusicSource == null) return;
            _root.MusicSource.Stop();
            _root.MusicSource.clip = null;
        }

        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (_disposed || clip == null) return;
            var source = Root()?.SfxSource;
            if (source != null)
                source.PlayOneShot(clip, ScaledVolume(AudioChannelId.Sfx, volumeScale));
        }

        public async UniTask PlaySfxAsync(string address, CancellationToken ct, float volumeScale = 1f)
        {
            var clip = await LoadClipAsync(address, ct);
            if (_disposed || ct.IsCancellationRequested || clip == null) return;
            PlaySfx(clip, volumeScale);
        }

        public void PlaySfxAt(AudioClip clip, Vector3 position, float volumeScale = 1f)
        {
            if (_disposed || clip == null) return;
            AudioSource.PlayClipAtPoint(clip, position, ScaledVolume(AudioChannelId.Sfx, volumeScale));
        }

        public void PlayUi(AudioClip clip, float volumeScale = 1f)
        {
            if (_disposed || clip == null) return;
            var source = Root()?.UiSource;
            if (source != null)
                source.PlayOneShot(clip, ScaledVolume(AudioChannelId.Ui, volumeScale));
        }

        public async UniTask PlayUiAsync(string address, CancellationToken ct, float volumeScale = 1f)
        {
            var clip = await LoadClipAsync(address, ct);
            if (_disposed || ct.IsCancellationRequested || clip == null) return;
            PlayUi(clip, volumeScale);
        }

        public void PlayAmbient(AudioClip clip, bool loop = true, bool restartIfSame = false)
        {
            if (_disposed || clip == null) return;

            var source = Root()?.AmbientSource;
            if (source == null) return;
            if (!restartIfSame && source.clip == clip && source.isPlaying) return;

            source.clip = clip;
            source.loop = loop;
            source.volume = ChannelVolume(AudioChannelId.Ambient);
            source.Play();
        }

        public async UniTask PlayAmbientAsync(
            string address, CancellationToken ct, bool loop = true, bool restartIfSame = false)
        {
            var clip = await LoadClipAsync(address, ct);
            if (_disposed || ct.IsCancellationRequested || clip == null) return;
            PlayAmbient(clip, loop, restartIfSame);
        }

        public void StopSfx()
        {
            if (_root == null) return;
            if (_root.SfxSource != null) _root.SfxSource.Stop();
            if (_root.UiSource != null) _root.UiSource.Stop();
        }

        public void StopAmbient()
        {
            if (_root == null || _root.AmbientSource == null) return;
            _root.AmbientSource.Stop();
            _root.AmbientSource.clip = null;
        }

        public void StopAll()
        {
            StopMusic();
            StopSfx();
            StopAmbient();
        }

        public void ReleaseCachedClips()
        {
            string[] addresses;
            lock (_clipCacheLock)
            {
                if (_clipCache.Count == 0) return;
                addresses = new string[_clipCache.Count];
                _clipCache.Keys.CopyTo(addresses, 0);
                _clipCache.Clear();
            }

            _clipLoader.ReleaseGroup(addresses);
        }

        public void SetMuted(bool muted)
        {
            if (_disposed) return;
            _muted = muted;
            ApplyVolumes();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _disposeCts.Cancel();
            CancelMusicFade();
            Audio.Clear(this);

            StopAll();
            if (_root != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Object.DestroyImmediate(_root.gameObject);
                else
                    Object.Destroy(_root.gameObject);
#else
                Object.Destroy(_root.gameObject);
#endif
                _root = null;
            }

            ReleaseCachedClips();
            _disposeCts.Dispose();
        }

        private async UniTask<AudioClip> LoadClipAsync(string address, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(address) || _disposed)
                return null;

            ct.ThrowIfCancellationRequested();

            lock (_clipCacheLock)
            {
                if (_clipCache.TryGetValue(address, out var cached))
                    return cached;
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _disposeCts.Token);
            var linkedToken = linkedCts.Token;
            linkedToken.ThrowIfCancellationRequested();

            AudioClip loaded;
            try
            {
                loaded = await _clipLoader.LoadAsync(address, linkedToken);
            }
            catch (OperationCanceledException) when (_disposed || _disposeCts.IsCancellationRequested)
            {
                return null;
            }

            if (loaded == null)
                return null;

            if (linkedToken.IsCancellationRequested || _disposed)
            {
                _clipLoader.Release(loaded);
                if (ct.IsCancellationRequested)
                    throw new OperationCanceledException(ct);
                return null;
            }

            lock (_clipCacheLock)
            {
                if (_clipCache.TryGetValue(address, out var cached))
                {
                    _clipLoader.Release(loaded);
                    return cached;
                }

                _clipCache[address] = loaded;
                return loaded;
            }
        }

        private AudioRoot Root()
        {
            if (_disposed) return null;

            if (_root == null)
            {
                _root = AudioRoot.Create();
                ApplyVolumes();
            }

            return _root;
        }

        private void ApplyVolumes()
        {
            if (_root == null) return;
            if (_root.MusicSource != null) _root.MusicSource.volume = ChannelVolume(AudioChannelId.Music) * _musicFadeScale;
            if (_root.AmbientSource != null) _root.AmbientSource.volume = ChannelVolume(AudioChannelId.Ambient);
            if (_root.SfxSource != null) _root.SfxSource.volume = 1f;
            if (_root.UiSource != null) _root.UiSource.volume = 1f;
        }

        private CancellationTokenSource StartMusicFade(CancellationToken ct)
        {
            CancelMusicFade();
            _musicFadeCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _disposeCts.Token);
            return _musicFadeCts;
        }

        private void CancelMusicFade()
        {
            if (_musicFadeCts == null) return;

            _musicFadeCts.Cancel();
            _musicFadeCts.Dispose();
            _musicFadeCts = null;
        }

        private async UniTask FadeMusicToAsync(float targetScale, float fadeSeconds, CancellationToken ct)
        {
            var startScale = _musicFadeScale;
            var elapsed = 0f;

            while (elapsed < fadeSeconds)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                SetMusicFadeScale(Mathf.Lerp(startScale, targetScale, Mathf.Clamp01(elapsed / fadeSeconds)));
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            SetMusicFadeScale(targetScale);
        }

        private void SetMusicFadeScale(float scale)
        {
            _musicFadeScale = Mathf.Clamp01(scale);
            ApplyVolumes();
        }

        private float ChannelVolume(AudioChannelId channel)
        {
            if (_muted) return 0f;
            return Mathf.Clamp01(_volumes.Master * GetVolume(channel));
        }

        private float ScaledVolume(AudioChannelId channel, float volumeScale)
        {
            return Mathf.Clamp01(ChannelVolume(channel) * Mathf.Clamp01(volumeScale));
        }

        private void ClampVolumes()
        {
            _volumes.Master = Mathf.Clamp01(_volumes.Master);
            _volumes.Music = Mathf.Clamp01(_volumes.Music);
            _volumes.Sfx = Mathf.Clamp01(_volumes.Sfx);
            _volumes.Ui = Mathf.Clamp01(_volumes.Ui);
            _volumes.Ambient = Mathf.Clamp01(_volumes.Ambient);
        }
    }
}
