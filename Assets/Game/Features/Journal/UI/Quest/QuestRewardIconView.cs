using System;
using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Quest.UI
{
    public sealed class QuestRewardIconView : MonoBehaviour, ICleanup
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _amountLabel;
        [SerializeField] private Button _infoButton;

        private Action<QuestRewardItemModel, RectTransform> _onInfo;
        private QuestRewardItemModel _model;
        private string _spriteId;
        public string SpriteId => _spriteId;

        private void Awake()
        {
            // The clickable area is normally the icon itself, so a Button on this GameObject is used
            // when the prefab does not wire a separate one.
            if (_infoButton == null) TryGetComponent(out _infoButton);
            if (_infoButton != null) _infoButton.onClick.AddListener(OnInfoClicked);
        }

        public void Bind(QuestRewardItemModel model, Action<QuestRewardItemModel, RectTransform> onInfo = null)
        {
            _model = model;
            _spriteId = model?.Id;
            _onInfo = !string.IsNullOrEmpty(_spriteId) ? onInfo : null;
            if (_amountLabel != null) _amountLabel.text = model != null ? model.Amount.ToString() : string.Empty;
            SetIconVisible(false);
            SetInfoInteractable(_onInfo != null);
        }

        public void SetIcon(Sprite sprite)
        {
            if (_icon != null) _icon.sprite = sprite;
            SetIconVisible(sprite != null);
        }

        public void Cleanup()
        {
            _onInfo = null;
            _model = null;
            _spriteId = null;
            if (_icon != null) _icon.sprite = null;
            if (_amountLabel != null) _amountLabel.text = string.Empty;
            SetIconVisible(false);
            SetInfoInteractable(false);
        }

        private void OnInfoClicked()
        {
            if (_model == null) return;

            var anchor = _infoButton != null && _infoButton.transform is RectTransform buttonRect
                ? buttonRect
                : transform as RectTransform;
            _onInfo?.Invoke(_model, anchor);
        }

        // Only interactability is toggled: the button often sits on the icon object itself, so
        // deactivating it would hide the reward.
        private void SetInfoInteractable(bool interactable)
        {
            if (_infoButton != null) _infoButton.interactable = interactable;
        }

        private void SetIconVisible(bool visible)
        {
            if (_icon != null) _icon.gameObject.SetActive(visible);
        }

        private void OnDestroy()
        {
            if (_infoButton != null) _infoButton.onClick.RemoveListener(OnInfoClicked);
        }
    }
}
