using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Decor.UI
{
    /// <summary>
    /// One decor characteristic chip (icon + label) — e.g. "standing", "small", "plant".
    /// Pooled via <see cref="UIListPool{T}"/> in <see cref="DecorInfoPopupView"/> and laid out in a
    /// scroll. The icon is a shared placeholder for now; later it will be loaded via Addressables by
    /// characteristic id (see <see cref="DecorInfoPopup"/>).
    /// </summary>
    public sealed class DecorCharacteristicItemView : MonoBehaviour, ICleanup
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _label;

        public void Bind(Sprite icon, string text)
        {
            if (_icon != null) _icon.sprite = icon;
            if (_label != null) _label.text = text;
        }

        // Called by UIListPool when the chip is (re)acquired or disabled so a pooled instance never
        // shows stale data.
        public void Cleanup()
        {
            if (_icon != null) _icon.sprite = null;
            if (_label != null) _label.text = string.Empty;
        }
    }
}
