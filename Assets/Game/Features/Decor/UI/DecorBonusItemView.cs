using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Decor.UI
{
    /// <summary>
    /// One decor bonus row (icon + description + signed percent) — e.g. "🎭 Drama … +2%".
    /// Wider than <see cref="DecorCharacteristicItemView"/>. Pooled via <see cref="UIListPool{T}"/>
    /// in <see cref="DecorInfoPopupView"/> and laid out in a scroll. The icon is a shared placeholder
    /// for now; later it will be loaded via Addressables by bonus id (see <see cref="DecorInfoPopup"/>).
    /// </summary>
    public sealed class DecorBonusItemView : MonoBehaviour, ICleanup
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _descriptionLabel;
        [SerializeField] private TextMeshProUGUI _percentLabel;

        public void Bind(Sprite icon, string description, string percent, Color percentColor)
        {
            if (_icon != null) _icon.sprite = icon;
            if (_descriptionLabel != null) _descriptionLabel.text = description;
            if (_percentLabel != null)
            {
                _percentLabel.text = percent;
                _percentLabel.color = percentColor;
            }
        }

        // Called by UIListPool when the row is (re)acquired or disabled so a pooled instance never
        // shows stale data.
        public void Cleanup()
        {
            if (_icon != null) _icon.sprite = null;
            if (_descriptionLabel != null) _descriptionLabel.text = string.Empty;
            if (_percentLabel != null) _percentLabel.text = string.Empty;
        }
    }
}
