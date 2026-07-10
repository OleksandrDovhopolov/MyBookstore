using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Infrastructure.Audio
{
    public interface IAudioService
    {
        AudioVolumeSettings Volumes { get; }

        void SetVolume(AudioChannelId channel, float volume);
        float GetVolume(AudioChannelId channel);

        void PlayMusic(AudioClip clip, bool loop = true, bool restartIfSame = false);
        UniTask PlayMusicAsync(string address, CancellationToken ct, bool loop = true, bool restartIfSame = false);
        void StopMusic();
        bool IsMusicPlaying { get; }

        void PlaySfx(AudioClip clip, float volumeScale = 1f);
        UniTask PlaySfxAsync(string address, CancellationToken ct, float volumeScale = 1f);
        void PlaySfxAt(AudioClip clip, Vector3 position, float volumeScale = 1f);
        void PlayUi(AudioClip clip, float volumeScale = 1f);
        UniTask PlayUiAsync(string address, CancellationToken ct, float volumeScale = 1f);
        void PlayAmbient(AudioClip clip, bool loop = true, bool restartIfSame = false);
        UniTask PlayAmbientAsync(string address, CancellationToken ct, bool loop = true, bool restartIfSame = false);

        void StopSfx();
        void StopAmbient();
        void StopAll();

        /// <summary>
        /// Releases Addressables clips cached by async play methods. Call only at a controlled point after
        /// stopping music/ambient; releasing clips while they are playing can unload assets under AudioSource.
        /// </summary>
        void ReleaseCachedClips();
        void SetMuted(bool muted);
    }
}
