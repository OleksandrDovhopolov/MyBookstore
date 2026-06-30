using UnityEngine;

namespace Infrastructure.Audio
{
    public interface IAudioService
    {
        AudioVolumeSettings Volumes { get; }

        void SetVolume(AudioChannelId channel, float volume);
        float GetVolume(AudioChannelId channel);

        void PlayMusic(AudioClip clip, bool loop = true, bool restartIfSame = false);
        void StopMusic();
        bool IsMusicPlaying { get; }

        void PlaySfx(AudioClip clip, float volumeScale = 1f);
        void PlaySfxAt(AudioClip clip, Vector3 position, float volumeScale = 1f);
        void PlayUi(AudioClip clip, float volumeScale = 1f);

        void StopSfx();
        void StopAll();
        void SetMuted(bool muted);
    }
}
