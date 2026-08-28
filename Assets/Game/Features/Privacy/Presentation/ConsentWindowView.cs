using System;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Privacy
{
    /// <summary>
    /// View for the first-run privacy notice. Deliberately has no close button: the prefab's
    /// WindowView._closeButtons array must stay empty so Accept is the only way out.
    /// </summary>
    public class ConsentWindowView : WindowView
    {
        public event Action AcceptClick;
        public event Action PrivacyLinkClick;

        [Header("Consent")]
        [SerializeField] private Button _acceptButton;
        [SerializeField] private Button _privacyLinkButton;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _bodyText;

        protected override void Awake()
        {
            base.Awake();

            if (_acceptButton != null)
            {
                _acceptButton.onClick.AddListener(() => AcceptClick?.Invoke());
            }

            if (_privacyLinkButton != null)
            {
                _privacyLinkButton.onClick.AddListener(() => PrivacyLinkClick?.Invoke());
            }
        }

        public void SetAcceptInteractable(bool interactable)
        {
            if (_acceptButton != null)
            {
                _acceptButton.interactable = interactable;
            }
        }

        public void SetPrivacyLinkVisible(bool visible)
        {
            if (_privacyLinkButton != null)
            {
                _privacyLinkButton.gameObject.SetActive(visible);
            }
        }

        public void SetTexts(string title, string body)
        {
            if (_titleText != null)
            {
                _titleText.text = title;
            }

            if (_bodyText != null)
            {
                _bodyText.text = body;
            }
        }
    }
}
