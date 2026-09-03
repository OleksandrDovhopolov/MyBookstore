using System;
using Game.Privacy.Services;
using Game.UI;
using Infrastructure.Audio;
using UnityEngine;
using VContainer;

namespace GameplayUI
{
    [Window("SettingsWindow", WindowType.Popup)]
    public sealed class SettingsWindowController : WindowController<SettingsWindowView>
    {
        private const string LogPrefix = "[Settings]";

        private IAudioService _audio;
        private PrivacyLinkSettings _links;

        [Inject]
        public void Construct(IAudioService audio, PrivacyLinkSettings links)
        {
            _audio = audio ?? throw new ArgumentNullException(nameof(audio));
            _links = links ?? throw new ArgumentNullException(nameof(links));
        }

        protected override void OnInit()
        {
            View.SoundChanged += OnSoundChanged;
            View.MusicChanged += OnMusicChanged;
            View.PrivacyTermsClicked += OnPrivacyTermsClicked;

            if (!_links.HasPrivacyPolicyUrl)
            {
                Debug.LogError(
                    $"{LogPrefix} Privacy policy URL is not configured on BootstrapInstaller. " +
                    "The Privacy & Terms button is hidden. This must be set before release.");
                View.SetPrivacyTermsVisible(false);
            }
        }

        protected override void OnShowStart() => RefreshToggleState();

        protected override void UpdateWindow() => RefreshToggleState();

        protected override void OnDispose()
        {
            if (View == null) return;

            View.SoundChanged -= OnSoundChanged;
            View.MusicChanged -= OnMusicChanged;
            View.PrivacyTermsClicked -= OnPrivacyTermsClicked;
        }

        private void RefreshToggleState()
        {
            View.SetSound(SettingsAudioToggleAdapter.IsSoundEnabled(_audio));
            View.SetMusic(SettingsAudioToggleAdapter.IsMusicEnabled(_audio));
        }

        private void OnSoundChanged(bool enabled)
            => SettingsAudioToggleAdapter.SetSoundEnabled(_audio, enabled);

        private void OnMusicChanged(bool enabled)
            => SettingsAudioToggleAdapter.SetMusicEnabled(_audio, enabled);

        private void OnPrivacyTermsClicked()
        {
            if (!_links.HasPrivacyPolicyUrl) return;

            Application.OpenURL(_links.TermsOfUseUrl);
        }
    }
}
