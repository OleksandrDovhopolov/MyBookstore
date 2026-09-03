using TMPro;
using UnityEngine;
using VContainer;

namespace Game.Localization
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string _key;
        [SerializeField] private string[] _formatArguments;

        private TMP_Text _label;
        private ILocalizationService _localization;

        [Inject]
        public void Construct(ILocalizationService localization)
        {
            _localization = localization;
            Refresh();
        }

        private void Awake()
        {
            _label = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            ResolveService();
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
                _label = GetComponent<TMP_Text>();
        }

        public void SetKey(string key)
        {
            _key = key;
            Refresh();
        }

        public void Refresh()
        {
            if (_label == null)
                _label = GetComponent<TMP_Text>();

            var service = _localization ?? LocalizationLocator.Service;
            if (_label == null || service == null)
                return;

            _label.text = _formatArguments != null && _formatArguments.Length > 0
                ? service.Get(_key, _formatArguments)
                : service.Get(_key);
        }

        private void ResolveService()
        {
            _localization ??= LocalizationLocator.Service;
        }

        private void Subscribe()
        {
            if (_localization != null)
                _localization.LocaleChanged += OnLocaleChanged;
        }

        private void Unsubscribe()
        {
            if (_localization != null)
                _localization.LocaleChanged -= OnLocaleChanged;
        }

        private void OnLocaleChanged(string _)
            => Refresh();
    }
}
