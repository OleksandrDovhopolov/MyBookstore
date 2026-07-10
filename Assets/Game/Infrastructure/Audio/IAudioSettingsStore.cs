namespace Infrastructure.Audio
{
    public interface IAudioSettingsStore
    {
        AudioVolumeSettings Load();
        void Save(AudioVolumeSettings settings);
    }
}
