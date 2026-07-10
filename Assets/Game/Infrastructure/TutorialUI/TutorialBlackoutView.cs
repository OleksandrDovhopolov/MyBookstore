using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Infrastructure.TutorialUI
{
    /// <summary>
    /// Full-screen dim built from four Image slices (top/bottom/left/right) around a rectangular hole. The
    /// hole has no graphic, so raycasts fall through it to the real UI beneath; the slices block everything
    /// else. No shader. Two modes: full cover (showText — any tap advances) and holed (highlightClick — only
    /// the hole is interactive). Lives on a RectTransform stretched to the overlay canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class TutorialBlackoutView : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>Fired on tap while in full-cover (tap-to-advance) mode.</summary>
        public event Action Tapped;

        private RectTransform _rt;
        private readonly Image[] _slices = new Image[4];
        private Color _color = new(0f, 0f, 0f, 0.7f);

        private RectTransform _followTarget;
        private float _followPadding;
        private bool _tapToAdvance;

        private RectTransform RectTransform => _rt != null ? _rt : _rt = (RectTransform)transform;

        private void Awake() => EnsureBuilt();

        public void Configure(Color color)
        {
            _color = color;
            EnsureBuilt();
            foreach (var slice in _slices)
                if (slice != null) slice.color = _color;
        }

        /// <summary>Cover the whole screen; taps advance (showText).</summary>
        public void ShowFullCover()
        {
            EnsureBuilt();
            _followTarget = null;
            _tapToAdvance = true;
            gameObject.SetActive(true);
            var r = RectTransform.rect;
            SetSlice(0, 0f, 0f, r.width, r.height);
            for (var i = 1; i < 4; i++) SetSlice(i, 0f, 0f, 0f, 0f);
        }

        /// <summary>Dim everything except a hole that tracks <paramref name="target"/> every frame (highlightClick).</summary>
        public void ShowWithHole(RectTransform target, float padding)
        {
            EnsureBuilt();
            _followTarget = target;
            _followPadding = padding;
            _tapToAdvance = false;
            gameObject.SetActive(true);
            UpdateHole();
        }

        public void HideView()
        {
            _followTarget = null;
            _tapToAdvance = false;
            gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_followTarget != null) UpdateHole();
        }

        private void UpdateHole()
        {
            if (_followTarget == null) return;
            if (!ScreenRectUtility.TryGetLocalRect(_followTarget, RectTransform, out var hole)) return;

            hole = new Rect(
                hole.x - _followPadding,
                hole.y - _followPadding,
                hole.width + 2f * _followPadding,
                hole.height + 2f * _followPadding);
            SetHole(hole);
        }

        private void SetHole(Rect holeLocal)
        {
            var r = RectTransform.rect;
            var w = r.width;
            var h = r.height;

            var left = Mathf.Clamp(holeLocal.xMin - r.xMin, 0f, w);
            var right = Mathf.Clamp(holeLocal.xMax - r.xMin, 0f, w);
            var bottom = Mathf.Clamp(holeLocal.yMin - r.yMin, 0f, h);
            var top = Mathf.Clamp(holeLocal.yMax - r.yMin, 0f, h);

            SetSlice(0, 0f, 0f, left, h);                 // left column
            SetSlice(1, right, 0f, w - right, h);          // right column
            SetSlice(2, left, top, right - left, h - top); // top middle
            SetSlice(3, left, 0f, right - left, bottom);   // bottom middle
        }

        private void SetSlice(int index, float x, float y, float w, float h)
        {
            var slice = _slices[index].rectTransform;
            slice.anchoredPosition = new Vector2(x, y);
            slice.sizeDelta = new Vector2(Mathf.Max(0f, w), Mathf.Max(0f, h));
        }

        private void EnsureBuilt()
        {
            if (_slices[0] != null) return;

            for (var i = 0; i < 4; i++)
            {
                var go = new GameObject($"Slice{i}", typeof(RectTransform), typeof(Image));
                var srt = (RectTransform)go.transform;
                srt.SetParent(RectTransform, false);
                srt.anchorMin = Vector2.zero;
                srt.anchorMax = Vector2.zero;
                srt.pivot = Vector2.zero;

                var img = go.GetComponent<Image>();
                img.color = _color;
                img.raycastTarget = true;
                _slices[i] = img;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_tapToAdvance) Tapped?.Invoke();
        }
    }
}
