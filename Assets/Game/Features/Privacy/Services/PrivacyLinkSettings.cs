namespace Game.Privacy.Services
{
    /// <summary>
    /// Public legal links shown on the first-run consent screen. Values come from BootstrapInstaller so
    /// legal can change the URL without a code change.
    /// </summary>
    public sealed class PrivacyLinkSettings
    {
        private readonly string _privacyPolicyUrl;
        private readonly string _termsOfUseUrl;

        public PrivacyLinkSettings(string privacyPolicyUrl, string termsOfUseUrl)
        {
            _privacyPolicyUrl = privacyPolicyUrl;
            _termsOfUseUrl = termsOfUseUrl;
        }

        public string PrivacyPolicyUrl => _privacyPolicyUrl;

        /// <summary>Falls back to the privacy policy URL when a combined page covers both documents.</summary>
        public string TermsOfUseUrl => string.IsNullOrWhiteSpace(_termsOfUseUrl)
            ? _privacyPolicyUrl
            : _termsOfUseUrl;

        public bool HasPrivacyPolicyUrl => !string.IsNullOrWhiteSpace(_privacyPolicyUrl);
    }
}
