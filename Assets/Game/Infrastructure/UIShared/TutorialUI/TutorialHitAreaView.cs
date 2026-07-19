using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Infrastructure.TutorialUI
{
    /// <summary>
    /// Transparent raycast target that tracks the blackout hole and forwards clicks to the real UI beneath it.
    /// Used for highlightClick targets that are panels/items rather than Buttons.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class TutorialHitAreaView : MonoBehaviour, IPointerClickHandler
    {
        public event Action Clicked;

        private readonly List<RaycastResult> _raycastResults = new();

        private RectTransform _rt;
        private Image _image;
        private RectTransform _followTarget;
        private float _followPadding;

        private RectTransform RectTransform => _rt != null ? _rt : _rt = (RectTransform)transform;
        private RectTransform ParentRect => transform.parent as RectTransform;

        private void Awake() => EnsureBuilt();

        public void PointAt(RectTransform target, float padding)
        {
            EnsureBuilt();
            _followTarget = target;
            _followPadding = padding;
            gameObject.SetActive(true);
            UpdateRect();
        }

        public void HideView()
        {
            _followTarget = null;
            gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_followTarget != null) UpdateRect();
        }

        private void UpdateRect()
        {
            var parent = ParentRect;
            if (_followTarget == null || parent == null) return;
            if (!ScreenRectUtility.TryGetLocalRect(_followTarget, parent, out var rect)) return;

            rect = new Rect(
                rect.x - _followPadding,
                rect.y - _followPadding,
                rect.width + 2f * _followPadding,
                rect.height + 2f * _followPadding);

            var parentRect = parent.rect;
            RectTransform.anchoredPosition = new Vector2(
                rect.xMin - parentRect.xMin,
                rect.yMin - parentRect.yMin);
            RectTransform.sizeDelta = new Vector2(Mathf.Max(0f, rect.width), Mathf.Max(0f, rect.height));
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            ForwardClick(eventData);
            Clicked?.Invoke();
        }

        private void ForwardClick(PointerEventData eventData)
        {
            if (eventData == null || EventSystem.current == null) return;

            EnsureBuilt();

            var wasRaycastTarget = _image.raycastTarget;
            _image.raycastTarget = false;
            try
            {
                _raycastResults.Clear();
                EventSystem.current.RaycastAll(eventData, _raycastResults);
                for (var i = 0; i < _raycastResults.Count; i++)
                {
                    var hit = _raycastResults[i].gameObject;
                    if (hit == null || IsSelfOrChild(hit.transform)) continue;

                    var handledBy = ExecuteEvents.ExecuteHierarchy<IPointerClickHandler>(
                        hit,
                        eventData,
                        ExecuteEvents.pointerClickHandler);
                    if (handledBy != null)
                        return;
                }
            }
            finally
            {
                _image.raycastTarget = wasRaycastTarget;
                _raycastResults.Clear();
            }
        }

        private bool IsSelfOrChild(Transform hit)
            => hit == transform || hit.IsChildOf(transform);

        private void EnsureBuilt()
        {
            var rt = RectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.localScale = Vector3.one;

            _image = _image != null ? _image : GetComponent<Image>();
            if (_image == null) _image = gameObject.AddComponent<Image>();
            _image.color = Color.clear;
            _image.raycastTarget = true;
        }
    }
}
