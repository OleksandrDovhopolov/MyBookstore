using System;
using Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GameplayUI
{
    public sealed class SettingsWindowView : WindowView
    {
        [Header("Settings")]
        [SerializeField] private UISwitch _soundSwitch;
        [SerializeField] private UISwitch _musicSwitch;
        [SerializeField] private Button _privacyTermsButton;

        private bool _soundEnabled;
        private bool _musicEnabled;

        public event Action<bool> SoundChanged;
        public event Action<bool> MusicChanged;
        public event Action PrivacyTermsClicked;

        protected override void Awake()
        {
            base.Awake();

            if (_soundSwitch != null)
                _soundSwitch.Init(OnSoundSwitchChanged, () => _soundEnabled);

            if (_musicSwitch != null)
                _musicSwitch.Init(OnMusicSwitchChanged, () => _musicEnabled);

            if (_privacyTermsButton != null)
                _privacyTermsButton.onClick.AddListener(OnPrivacyTermsClicked);
        }

        protected override void OnDestroy()
        {
            if (_privacyTermsButton != null)
                _privacyTermsButton.onClick.RemoveListener(OnPrivacyTermsClicked);

            base.OnDestroy();
        }

        public void SetSound(bool enabled)
        {
            _soundEnabled = enabled;

            if (_soundSwitch != null)
                _soundSwitch.SetIsOnWithoutNotify(enabled);
        }

        public void SetMusic(bool enabled)
        {
            _musicEnabled = enabled;

            if (_musicSwitch != null)
                _musicSwitch.SetIsOnWithoutNotify(enabled);
        }

        public void SetPrivacyTermsVisible(bool visible)
        {
            if (_privacyTermsButton != null)
                _privacyTermsButton.gameObject.SetActive(visible);
        }

        private void OnSoundSwitchChanged(bool value)
        {
            _soundEnabled = value;
            SoundChanged?.Invoke(value);
        }

        private void OnMusicSwitchChanged(bool value)
        {
            _musicEnabled = value;
            MusicChanged?.Invoke(value);
        }

        private void OnPrivacyTermsClicked() => PrivacyTermsClicked?.Invoke();
    }
}
