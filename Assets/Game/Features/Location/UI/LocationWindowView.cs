using System;
using System.Collections.Generic;
using Game.UI;
using Game.UI.ContentWidget;
using SpriteService;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Location.UI
{
    public sealed class LocationWindowView : WindowView
    {
        // Pixels the content must travel before the scroll counts as "the player moved the list".
        private const float ScrollMoveThresholdPixels = 2f;

        [Header("List")]
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private UIListPool<LocationRowView> _rowPool = new();
        [SerializeField] private LocationDemandWidgetView _demandWidgetPrefab;
        [SerializeField] private LocationRequirementInfoWidgetView _requirementInfoWidgetPrefab;

        private Action _onScrolled;
        private Vector2 _lastContentPosition;

        protected override void Awake()
        {
            base.Awake();
            if (_scrollRect != null)
            {
                _lastContentPosition = GetContentPosition();
                _scrollRect.onValueChanged.AddListener(OnScrollChanged);
            }

            if (_demandWidgetPrefab != null)
                WidgetRegistry.Register<LocationDemandWidgetData>(_demandWidgetPrefab);
            if (_requirementInfoWidgetPrefab != null)
                WidgetRegistry.Register<LocationRequirementInfoWidgetData>(_requirementInfoWidgetPrefab);
        }

        public void Render(IReadOnlyList<LocationListItemModel> models, Action<string> onStart, Action<string> onUnlock,
            Action<string, RectTransform> onDemandInfo,
            Action<LocationRequirementRef, RectTransform> onRequirementInfo,
            Action onScrolled,
            IUiSpriteProvider sprites)
        {
            _onScrolled = onScrolled;
            _rowPool.DisableAll();

            if (models != null)
            {
                for (var i = 0; i < models.Count; i++)
                {
                    var model = models[i];
                    if (model == null) continue;
                    _rowPool.GetNext().Bind(model, onStart, onUnlock, onDemandInfo, onRequirementInfo, sprites);
                }
            }

            _rowPool.DisableNonActive();
        }

        public void Clear()
        {
            _onScrolled = null;
            _rowPool.DisableAll();
        }

        private void OnScrollChanged(Vector2 _)
        {
            // Content position, not normalizedPosition: the latter degenerates into a constant 0/1 step
            // when the content fits the viewport and saturates at the list edges, so real drags there
            // produced no delta at all and the demand widget stayed open.
            var position = GetContentPosition();
            if ((position - _lastContentPosition).sqrMagnitude
                <= ScrollMoveThresholdPixels * ScrollMoveThresholdPixels)
                return;

            _lastContentPosition = position;
            _onScrolled?.Invoke();
        }

        private Vector2 GetContentPosition()
        {
            var content = _scrollRect != null ? _scrollRect.content : null;
            return content != null ? content.anchoredPosition : Vector2.zero;
        }

        protected override void OnDestroy()
        {
            if (_scrollRect != null)
                _scrollRect.onValueChanged.RemoveListener(OnScrollChanged);

            base.OnDestroy();
        }
    }
}
