using UnityEngine;

namespace Game.Decor.UI
{
    /// <summary>
    /// Scales an overlay authored in source-art coordinates so its children stay aligned with
    /// an aspect-managed target rect across aspect ratios and safe-area changes.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class ReferenceSizeOverlayScaler : MonoBehaviour
    {
        [SerializeField] private RectTransform _targetRect;
        [SerializeField] private Vector2 _referenceSize = new Vector2(783f, 1618f);

        private const float Epsilon = 0.01f;
        private static readonly Vector2 CenterAnchor = new Vector2(0.5f, 0.5f);

        private RectTransform _rectTransform;
        private Vector2 _lastTargetSize;
        private Vector2 _lastReferenceSize;

        private void OnEnable()
        {
            Apply(force: true);
        }

        private void LateUpdate()
        {
            Apply(force: false);
        }

        private void OnRectTransformDimensionsChange()
        {
            Apply(force: false);
        }

        private void OnValidate()
        {
            Apply(force: true);
        }

        private void Apply(bool force)
        {
            CacheReferences();

            if (_rectTransform == null || _targetRect == null) return;
            if (!IsValid(_referenceSize)) return;

            Vector2 targetSize = _targetRect.rect.size;
            if (!IsValid(targetSize)) return;

            if (!force
                && Approximately(targetSize, _lastTargetSize)
                && Approximately(_referenceSize, _lastReferenceSize))
            {
                return;
            }

            _lastTargetSize = targetSize;
            _lastReferenceSize = _referenceSize;

            _rectTransform.anchorMin = CenterAnchor;
            _rectTransform.anchorMax = CenterAnchor;
            _rectTransform.pivot = CenterAnchor;
            _rectTransform.anchoredPosition = Vector2.zero;
            _rectTransform.sizeDelta = _referenceSize;

            float scale = Mathf.Min(
                targetSize.x / _referenceSize.x,
                targetSize.y / _referenceSize.y);

            _rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        private void CacheReferences()
        {
            if (_rectTransform == null)
                _rectTransform = GetComponent<RectTransform>();

            if (_targetRect == null && transform.parent is RectTransform parent)
                _targetRect = parent;
        }

        private static bool IsValid(Vector2 size) =>
            size.x > Epsilon && size.y > Epsilon;

        private static bool Approximately(Vector2 a, Vector2 b) =>
            Mathf.Abs(a.x - b.x) < Epsilon && Mathf.Abs(a.y - b.y) < Epsilon;
    }
}
