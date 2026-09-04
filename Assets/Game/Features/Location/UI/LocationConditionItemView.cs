using System;
using TMPro;
using Game.LocationUnlock.API;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Location.UI
{
    public sealed class LocationConditionItemView : MonoBehaviour, ICleanup
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _countLabel;
        [SerializeField] private Button _button;

        private string _spriteId;
        private LocationRequirementRef _requirement;
        private Action<LocationRequirementRef, RectTransform> _onInfoClicked;

        public string SpriteId => _spriteId;

        private void Awake()
        {
            if (_button != null)
                _button.onClick.AddListener(OnClicked);
        }

        public void Bind(
            LocationConditionProgress progress,
            Action<LocationRequirementRef, RectTransform> onInfoClicked = null)
        {
            _spriteId = progress.SpriteId;
            _requirement = LocationRequirementRef.Condition(progress.ReasonKey);
            _onInfoClicked = onInfoClicked;
            SetIconVisible(!string.IsNullOrEmpty(_spriteId));
            if (_countLabel != null) _countLabel.text = $"{progress.Current}/{progress.Target}";
        }

        public void Bind(
            LocationUnlockCostProgress progress,
            Action<LocationRequirementRef, RectTransform> onInfoClicked = null)
        {
            _spriteId = progress.ItemId;
            _requirement = LocationRequirementRef.Item(progress.ItemId);
            _onInfoClicked = onInfoClicked;
            SetIconVisible(!string.IsNullOrEmpty(_spriteId));
            if (_countLabel != null) _countLabel.text = $"{progress.Have}/{progress.Need}";
        }

        public void SetIcon(Sprite sprite)
        {
            if (_icon != null) _icon.sprite = sprite;
            SetIconVisible(sprite != null);
        }

        public void Cleanup()
        {
            _spriteId = null;
            _requirement = default;
            _onInfoClicked = null;
            if (_icon != null) _icon.sprite = null;
            if (_countLabel != null) _countLabel.text = string.Empty;
            SetIconVisible(false);
        }

        private void OnClicked()
        {
            if (_onInfoClicked == null)
                return;

            if (_requirement.Kind == LocationRequirementKind.Item && string.IsNullOrEmpty(_requirement.Key))
                return;

            _onInfoClicked.Invoke(_requirement, transform as RectTransform);
        }

        private void SetIconVisible(bool visible)
        {
            if (_icon != null) _icon.gameObject.SetActive(visible);
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnClicked);
        }
    }
}
