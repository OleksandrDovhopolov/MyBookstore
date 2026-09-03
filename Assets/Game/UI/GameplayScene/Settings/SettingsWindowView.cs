using System;
using Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GameplayUI
{
    public sealed class SettingsWindowView : WindowView
    {
        [Header("Settings")]
        [SerializeField] private Toggle _soundToggle;
        [SerializeField] private Toggle _musicToggle;
        [SerializeField] private Button _privacyTermsButton;

        public event Action<bool> SoundChanged;
        public event Action<bool> MusicChanged;
        public event Action PrivacyTermsClicked;

        protected override void Awake()
        {
            base.Awake();

            if (_soundToggle != null)
                _soundToggle.onValueChanged.AddListener(OnSoundToggleChanged);

            if (_musicToggle != null)
                _musicToggle.onValueChanged.AddListener(OnMusicToggleChanged);

            if (_privacyTermsButton != null)
                _privacyTermsButton.onClick.AddListener(OnPrivacyTermsClicked);
        }

        protected override void OnDestroy()
        {
            if (_soundToggle != null)
                _soundToggle.onValueChanged.RemoveListener(OnSoundToggleChanged);

            if (_musicToggle != null)
                _musicToggle.onValueChanged.RemoveListener(OnMusicToggleChanged);

            if (_privacyTermsButton != null)
                _privacyTermsButton.onClick.RemoveListener(OnPrivacyTermsClicked);

            base.OnDestroy();
        }

        public void SetSound(bool enabled)
        {
            if (_soundToggle != null)
                _soundToggle.SetIsOnWithoutNotify(enabled);
        }

        public void SetMusic(bool enabled)
        {
            if (_musicToggle != null)
                _musicToggle.SetIsOnWithoutNotify(enabled);
        }

        public void SetPrivacyTermsVisible(bool visible)
        {
            if (_privacyTermsButton != null)
                _privacyTermsButton.gameObject.SetActive(visible);
        }

        private void OnSoundToggleChanged(bool value) => SoundChanged?.Invoke(value);
        private void OnMusicToggleChanged(bool value) => MusicChanged?.Invoke(value);
        private void OnPrivacyTermsClicked() => PrivacyTermsClicked?.Invoke();
    }
}
