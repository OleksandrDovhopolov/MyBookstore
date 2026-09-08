namespace Game.Privacy.Services
{
    /// <summary>
    /// Public legal links shown on the first-run consent screen. Values come from BootstrapInstaller so
    /// legal can change the URL without a code change.
    /// </summary>
    public sealed class PrivacyLinkSettings
    {
        private readonly string _privacyPolicyUrl;

        public PrivacyLinkSettings(string privacyPolicyUrl)
        {
            _privacyPolicyUrl = privacyPolicyUrl;
        }

        public string PrivacyPolicyUrl => _privacyPolicyUrl;

        public bool HasPrivacyPolicyUrl => !string.IsNullOrWhiteSpace(_privacyPolicyUrl);
    }
}
