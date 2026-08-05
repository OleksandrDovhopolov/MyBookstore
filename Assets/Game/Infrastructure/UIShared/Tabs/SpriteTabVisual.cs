using UnityEngine;
using UnityEngine.UI;

namespace UIShared
{
    /// <summary>Swaps the tab background sprite between the normal and the selected state.</summary>
    public sealed class SpriteTabVisual : MonoBehaviour, ITabButtonVisual
    {
        [SerializeField] private Image _targetImage;
        [SerializeField] private Sprite _normalSprite;
        [SerializeField] private Sprite _selectedSprite;

        public void ApplySelected(bool selected)
        {
            if (_targetImage == null)
                return;

            var sprite = selected ? _selectedSprite : _normalSprite;
            if (sprite != null)
                _targetImage.sprite = sprite;
        }
    }
}
