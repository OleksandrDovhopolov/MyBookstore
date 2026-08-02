using UnityEngine;
using UnityEngine.UI;

namespace Game.Inventory.UI
{
    /// <summary>
    /// Normalizes icon art of mixed source sizes to one on-screen size. Resizes the Image's rect so the
    /// sprite is inscribed in <see cref="_frame"/> with its longest side equal to the frame's shorter
    /// side times <see cref="_fillFactor"/> — a 500x500 and a 419x596 source then render at the same
    /// height instead of 200 vs 275.
    /// Only the sprite's aspect ratio is used, so platform texture downsampling (Android
    /// maxTextureSize) cannot change the result. Drives sizeDelta rather than localScale, so scale
    /// tweens and layout groups stay untouched.
    /// The sprite is loaded asynchronously, so the owner must call <see cref="Fit"/> after assigning it
    /// (see <see cref="InventoryItemRowView"/>).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class InventoryIconFitter : MonoBehaviour
    {
        private const float Epsilon = 0.01f;
        private static readonly Vector2 CenterAnchor = new Vector2(0.5f, 0.5f);

        [Tooltip("Rect the icon is inscribed into. Left empty, the parent RectTransform is used.")]
        [SerializeField] private RectTransform _frame;

        [Tooltip("Share of the frame's shorter side taken by the icon's longest side. 1 = touches the frame.")]
        [SerializeField, Range(0.1f, 1f)] private float _fillFactor = 1f;

        private Image _image;
        private RectTransform _rectTransform;
        private RectTransform _resolvedFrame;

        private void OnEnable()
        {
            Fit();
        }

        private void OnValidate()
        {
            Fit();
        }

        /// <summary>
        /// Re-measures the current sprite and resizes the rect. Safe to call with no sprite assigned —
        /// the rect is then left as authored.
        /// </summary>
        public void Fit()
        {
            if (!CacheReferences()) return;

            var sprite = _image.sprite;
            if (sprite == null) return;

            var spriteSize = sprite.rect.size;
            if (spriteSize.x < Epsilon || spriteSize.y < Epsilon) return;

            var frameSize = _resolvedFrame.rect.size;
            if (frameSize.x < Epsilon || frameSize.y < Epsilon) return;

            // Measuring against the shorter side guarantees the icon fits the frame on both axes.
            var reference = Mathf.Min(frameSize.x, frameSize.y) * _fillFactor;
            var scale = reference / Mathf.Max(spriteSize.x, spriteSize.y);

            _rectTransform.anchorMin = CenterAnchor;
            _rectTransform.anchorMax = CenterAnchor;
            _rectTransform.pivot = CenterAnchor;
            _rectTransform.anchoredPosition = Vector2.zero;
            _rectTransform.sizeDelta = spriteSize * scale;
        }

        private bool CacheReferences()
        {
            if (_image == null) TryGetComponent(out _image);
            if (_rectTransform == null) _rectTransform = transform as RectTransform;

            // Resolved separately from the serialized field so the fallback never dirties the prefab.
            _resolvedFrame = _frame != null ? _frame : transform.parent as RectTransform;

            return _image != null && _rectTransform != null && _resolvedFrame != null;
        }
    }
}
