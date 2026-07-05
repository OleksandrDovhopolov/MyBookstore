using TMPro;
using UnityEngine;

namespace Game.Tutorial.Presentation
{
    /// <summary>
    /// Text bubble shown by the tutorial overlay. Prefab root carries this component + a TMP_Text and an
    /// optional background. Placement anchors the panel to the top or bottom of the safe area.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class TutorialTextPanelView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;

        private RectTransform _rt;
        private RectTransform RectTransform => _rt != null ? _rt : _rt = (RectTransform)transform;

        public void SetText(string value, string placement)
        {
            if (_text != null) _text.text = value ?? string.Empty;
            ApplyPlacement(placement);
            gameObject.SetActive(true);
        }

        public void HideView() => gameObject.SetActive(false);

        private void ApplyPlacement(string placement)
        {
            // "bottom" (default) / "aboveTarget" → anchor near the bottom; "top" → near the top. Kept simple;
            // precise anchoring vs a target rect can come later.
            var top = string.Equals(placement, "top", System.StringComparison.OrdinalIgnoreCase);
            var y = top ? 1f : 0f;
            RectTransform.anchorMin = new Vector2(0.5f, y);
            RectTransform.anchorMax = new Vector2(0.5f, y);
            RectTransform.pivot = new Vector2(0.5f, y);
            RectTransform.anchoredPosition = new Vector2(0f, top ? -120f : 120f);
        }
    }
}
