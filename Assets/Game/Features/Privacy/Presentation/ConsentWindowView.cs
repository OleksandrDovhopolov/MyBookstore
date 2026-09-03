using System;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Privacy
{
    /// <summary>
    /// View for the first-run privacy notice. Deliberately has no close button: the prefab's
    /// WindowView._closeButtons array must stay empty so Accept/Decline are the only ways out.
    /// </summary>
    public class ConsentWindowView : WindowView
    {
        public event Action AcceptClick;
        public event Action DeclineClick;
        public event Action PrivacyLinkClick;

        [Header("Consent")]
        [SerializeField] private Button _acceptButton;
        [SerializeField] private Button _declineButton;
        [SerializeField] private Button _privacyLinkButton;
        [SerializeField] private TextMeshProUGUI _bodyText;

        protected override void Awake()
        {
            base.Awake();

            if (_acceptButton != null)
            {
                _acceptButton.onClick.AddListener(() => AcceptClick?.Invoke());
            }

            if (_declineButton != null)
            {
                _declineButton.onClick.AddListener(() => DeclineClick?.Invoke());
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

        public void SetDeclineInteractable(bool interactable)
        {
            if (_declineButton != null)
            {
                _declineButton.interactable = interactable;
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
