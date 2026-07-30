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

        private string _spriteId;
        public string SpriteId => _spriteId;

        public void Bind(LocationConditionProgress progress)
        {
            _spriteId = progress.SpriteId;
            SetIconVisible(!string.IsNullOrEmpty(_spriteId));
            if (_countLabel != null) _countLabel.text = $"{progress.Current}/{progress.Target}";
        }

        public void Bind(LocationUnlockCostProgress progress)
        {
            _spriteId = progress.ItemId;
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
            if (_icon != null) _icon.sprite = null;
            SetIconVisible(false);
        }

        private void SetIconVisible(bool visible)
        {
            if (_icon != null) _icon.gameObject.SetActive(visible);
        }
    }
}
