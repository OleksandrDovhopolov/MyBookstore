using UnityEngine;

namespace Infrastructure
{
    /// <summary>
    /// Inverse of <see cref="SafeAreaLayout"/>. Stretches its <see cref="RectTransform"/> to cover
    /// the FULL physical screen even though it lives under a safe-area-inset parent — letting a
    /// full-bleed background escape the safe area while sibling chrome stays inside it.
    ///
    /// Correct ONLY when the parent rect already equals the safe area (e.g. a window root parented
    /// under SafeAreaRoot). It expresses the whole screen in the parent's anchor-fraction space, so
    /// the anchors go negative / above 1 to extend past the parent. Resolution-independent, so it is
    /// CanvasScaler-agnostic.
    /// </summary>
    [DisallowMultipleComponent]
    public class SafeAreaInverseStretch : MonoBehaviour
    {
        private RectTransform _panel;
        private Rect _lastSafeArea;
        private Vector2 _lastScreenSize;

        private void OnEnable()
        {
            _panel = GetComponent<RectTransform>();
            Invalidate();
            Refresh();
        }

        // LateUpdate (not Update): SafeAreaLayout mutates the parent's rect in Update, so recompute
        // after that has run for the frame to avoid a one-frame ordering race.
        private void LateUpdate() => Refresh();

        private void OnRectTransformDimensionsChange() => Refresh();

        private void Invalidate()
        {
            _lastSafeArea = new Rect(0f, 0f, 0f, 0f);
            _lastScreenSize = Vector2.zero;
        }

        private void Refresh()
        {
            if (_panel == null) _panel = GetComponent<RectTransform>();
            if (_panel == null) return;

            var safeArea = Screen.safeArea;
            var screenSize = new Vector2(Screen.width, Screen.height);
            if (screenSize.x <= 0f || screenSize.y <= 0f) return;

            // Guard on both safe area and screen size — the mapping normalises by screen size, and
            // on orientation/resolution changes one can update a frame before the other.
            if (safeArea == _lastSafeArea && screenSize == _lastScreenSize) return;

            _lastSafeArea = safeArea;
            _lastScreenSize = screenSize;
            Apply(safeArea, screenSize);
        }

        private void Apply(Rect safeArea, Vector2 screenSize)
        {
            var safeMin = new Vector2(safeArea.xMin / screenSize.x, safeArea.yMin / screenSize.y);
            var safeMax = new Vector2(safeArea.xMax / screenSize.x, safeArea.yMax / screenSize.y);

            var span = safeMax - safeMin;
            if (span.x <= Mathf.Epsilon || span.y <= Mathf.Epsilon) return; // degenerate safe area

            // Parent == safe area. Map the full screen (0..1 of the screen) into the parent's local
            // anchor fractions; values outside [0,1] push the rect past the parent to the screen edges.
            _panel.anchorMin = new Vector2((0f - safeMin.x) / span.x, (0f - safeMin.y) / span.y);
            _panel.anchorMax = new Vector2((1f - safeMin.x) / span.x, (1f - safeMin.y) / span.y);
            _panel.offsetMin = Vector2.zero;
            _panel.offsetMax = Vector2.zero;
        }
    }
}
