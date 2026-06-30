using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Infrastructure.Audio
{
    public sealed class AudioService : IAudioService, IDisposable
    {
        private readonly IAudioSettingsStore _settingsStore;
        private readonly AudioVolumeSettings _volumes;

        private AudioRoot _root;
        private bool _muted;

        public AudioService(IAudioSettingsStore settingsStore)
        {
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
            _volumes = (_settingsStore.Load() ?? new AudioVolumeSettings()).Clone();
            ClampVolumes();
        }

        public AudioVolumeSettings Volumes => _volumes.Clone();

        public bool IsMusicPlaying => _root != null && _root.MusicSource != null && _root.MusicSource.isPlaying;

        public void SetVolume(AudioChannelId channel, float volume)
        {
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
                _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, null)
            };
        }

        public void PlayMusic(AudioClip clip, bool loop = true, bool restartIfSame = false)
        {
            if (clip == null) return;

            var source = Root().MusicSource;
            if (!restartIfSame && source.clip == clip && source.isPlaying) return;

            source.clip = clip;
            source.loop = loop;
            source.volume = ChannelVolume(AudioChannelId.Music);
            source.Play();
        }

        public void StopMusic()
        {
            if (_root == null || _root.MusicSource == null) return;
            _root.MusicSource.Stop();
            _root.MusicSource.clip = null;
        }

        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return;
            Root().SfxSource.PlayOneShot(clip, ScaledVolume(AudioChannelId.Sfx, volumeScale));
        }

        public void PlaySfxAt(AudioClip clip, Vector3 position, float volumeScale = 1f)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, position, ScaledVolume(AudioChannelId.Sfx, volumeScale));
        }

        public void PlayUi(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return;
            Root().UiSource.PlayOneShot(clip, ScaledVolume(AudioChannelId.Ui, volumeScale));
        }

        public void StopSfx()
        {
            if (_root == null) return;
            if (_root.SfxSource != null) _root.SfxSource.Stop();
            if (_root.UiSource != null) _root.UiSource.Stop();
        }

        public void StopAll()
        {
            StopMusic();
            StopSfx();
        }

        public void SetMuted(bool muted)
        {
            _muted = muted;
            ApplyVolumes();
        }

        public void Dispose()
        {
            Audio.Clear(this);

            if (_root == null) return;
            Object.Destroy(_root.gameObject);
            _root = null;
        }

        private AudioRoot Root()
        {
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
            if (_root.MusicSource != null) _root.MusicSource.volume = ChannelVolume(AudioChannelId.Music);
            if (_root.SfxSource != null) _root.SfxSource.volume = ChannelVolume(AudioChannelId.Sfx);
            if (_root.UiSource != null) _root.UiSource.volume = ChannelVolume(AudioChannelId.Ui);
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
        }
    }
}
