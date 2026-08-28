namespace Analytics
{
    /// <summary>
    /// Version of the privacy policy / terms the first-run consent screen presents.
    ///
    /// Bump this whenever the wording of the policy changes in a way users must see again: every player
    /// with an older recorded decision is re-prompted, and their previously granted flags stop counting
    /// until they accept again (see <see cref="ConsentService"/>).
    ///
    /// Do not confuse this with the ".v1" suffix on the PlayerPrefs keys in
    /// <see cref="PlayerPrefsConsentStore"/> — that is the storage layout version and changes only when
    /// the set or types of the keys change.
    /// </summary>
    public static class ConsentPolicy
    {
        public const int Version = 1;
    }
}
