using Infrastructure.Audio;

namespace GameplayUI
{
    public static class SettingsAudioToggleAdapter
    {
        public static bool IsSoundEnabled(IAudioService audio)
            => audio != null
               && (audio.GetVolume(AudioChannelId.Sfx) > 0f
                   || audio.GetVolume(AudioChannelId.Ui) > 0f);

        public static bool IsMusicEnabled(IAudioService audio)
            => audio != null
               && (audio.GetVolume(AudioChannelId.Music) > 0f
                   || audio.GetVolume(AudioChannelId.Ambient) > 0f);

        public static void SetSoundEnabled(IAudioService audio, bool enabled)
        {
            if (audio == null) return;

            var volume = enabled ? 1f : 0f;
            audio.SetVolume(AudioChannelId.Sfx, volume);
            audio.SetVolume(AudioChannelId.Ui, volume);
        }

        public static void SetMusicEnabled(IAudioService audio, bool enabled)
        {
            if (audio == null) return;

            var volume = enabled ? 1f : 0f;
            audio.SetVolume(AudioChannelId.Music, volume);
            audio.SetVolume(AudioChannelId.Ambient, volume);
        }
    }
}
