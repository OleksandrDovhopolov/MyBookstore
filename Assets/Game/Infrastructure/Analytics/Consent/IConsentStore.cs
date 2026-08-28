namespace Analytics
{
    /// <summary>
    /// Persistence for the consent decision. Split out from <see cref="ConsentService"/> so the service
    /// can be unit tested without touching PlayerPrefs — same split as IAudioSettingsStore.
    /// </summary>
    public interface IConsentStore
    {
        bool TryLoad(out ConsentRecord record);
        void Save(in ConsentRecord record);
    }
}
