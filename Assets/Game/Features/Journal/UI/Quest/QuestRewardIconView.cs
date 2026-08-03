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

        private string _spriteId;
        public string SpriteId => _spriteId;

        public void Bind(QuestRewardItemModel model)
        {
            _spriteId = model?.Id;
            if (_amountLabel != null) _amountLabel.text = model != null ? model.Amount.ToString() : string.Empty;
            SetIconVisible(false);
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
            if (_amountLabel != null) _amountLabel.text = string.Empty;
            SetIconVisible(false);
        }

        private void SetIconVisible(bool visible)
        {
            if (_icon != null) _icon.gameObject.SetActive(visible);
        }
    }
}
