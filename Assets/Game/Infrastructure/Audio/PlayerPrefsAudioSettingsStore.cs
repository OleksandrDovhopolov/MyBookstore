using UnityEngine;

namespace Infrastructure.Audio
{
    public sealed class PlayerPrefsAudioSettingsStore : IAudioSettingsStore
    {
        private const string MasterKey = "audio.master";
        private const string MusicKey = "audio.music";
        private const string SfxKey = "audio.sfx";
        private const string UiKey = "audio.ui";

        public AudioVolumeSettings Load()
        {
            return new AudioVolumeSettings
            {
                Master = PlayerPrefs.GetFloat(MasterKey, 1f),
                Music = PlayerPrefs.GetFloat(MusicKey, 1f),
                Sfx = PlayerPrefs.GetFloat(SfxKey, 1f),
                Ui = PlayerPrefs.GetFloat(UiKey, 1f)
            };
        }

        public void Save(AudioVolumeSettings settings)
        {
            settings ??= new AudioVolumeSettings();
            PlayerPrefs.SetFloat(MasterKey, Mathf.Clamp01(settings.Master));
            PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(settings.Music));
            PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(settings.Sfx));
            PlayerPrefs.SetFloat(UiKey, Mathf.Clamp01(settings.Ui));
            PlayerPrefs.Save();
        }
    }
}
