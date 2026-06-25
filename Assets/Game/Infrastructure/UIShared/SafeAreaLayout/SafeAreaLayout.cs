using UnityEngine;

namespace Infrastructure
{
    public class SafeAreaLayout : MonoBehaviour
    {
        private RectTransform _panel;
        private Rect _lastSafeArea;

        private void OnEnable()
        {
            _lastSafeArea = new Rect(0, 0, 0, 0);
            _panel = GetComponent<RectTransform>();
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            Rect safeArea = GetSafeArea();

            if (safeArea != _lastSafeArea) ApplySafeArea(safeArea);
        }

        private Rect GetSafeArea()
        {
            var area = Screen.safeArea;
            return area;
        }

        private void ApplySafeArea(Rect r)
        {
            _lastSafeArea = r;

            var anchorMin = r.position;
            var anchorMax = r.position + r.size;
            anchorMin.x /= Screen.width;
            anchorMax.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.y /= Screen.height;

            _panel.anchorMin = anchorMin;
            _panel.anchorMax = anchorMax;

            // Zero the offsets so the rect lands exactly on the safe area regardless of any
            // sizeDelta/anchoredPosition the panel had — anchors alone don't guarantee that.
            _panel.offsetMin = Vector2.zero;
            _panel.offsetMax = Vector2.zero;
        }
    }
}
