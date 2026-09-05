using System;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Privacy
{
    /// <summary>
    /// View for the first-run privacy notice. Deliberately has no close button: the prefab's
    /// WindowView._closeButtons array must stay empty so Continue is the only way out.
    /// </summary>
    public class ConsentWindowView : WindowView
    {
        public event Action ContinueClick;
        public event Action PrivacyLinkClick;

        [Header("Consent")]
        [SerializeField] private Button _continueButton;
        [SerializeField] private Toggle _analyticsToggle;
        [SerializeField] private Button _privacyLinkButton;
        [SerializeField] private TextMeshProUGUI _bodyText;

        public bool AnalyticsConsent => _analyticsToggle != null && _analyticsToggle.isOn;

        protected override void Awake()
        {
            base.Awake();

            if (_continueButton != null)
            {
                _continueButton.onClick.AddListener(() => ContinueClick?.Invoke());
            }

            if (_privacyLinkButton != null)
            {
                _privacyLinkButton.onClick.AddListener(() => PrivacyLinkClick?.Invoke());
            }
        }

        public void SetContinueInteractable(bool interactable)
        {
            if (_continueButton != null)
            {
                _continueButton.interactable = interactable;
            }
        }

        public void SetAnalyticsConsent(bool on)
        {
            if (_analyticsToggle != null)
            {
                _analyticsToggle.SetIsOnWithoutNotify(on);
            }
        }

        public void SetPrivacyLinkVisible(bool visible)
        {
            if (_privacyLinkButton != null)
            {
                _privacyLinkButton.gameObject.SetActive(visible);
            }
        }

        public void SetTexts(string body)
        {
            if (_bodyText != null)
            {
                _bodyText.text = body;
            }
        }
    }
}
