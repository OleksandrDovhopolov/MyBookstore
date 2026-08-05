using UnityEngine;
using UnityEngine.UI;

namespace UIShared
{
    public sealed class SpriteTabButton : TabButton
    {
        [SerializeField] private Image _targetImage;
        [SerializeField] private Sprite _normalSprite;
        [SerializeField] private Sprite _selectedSprite;

        protected override void ApplySelected(bool selected)
        {
            if (_targetImage == null)
                return;

            var sprite = selected ? _selectedSprite : _normalSprite;
            if (sprite != null)
                _targetImage.sprite = sprite;
        }
    }
}
