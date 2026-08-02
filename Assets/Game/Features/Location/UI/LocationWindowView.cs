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
        [Header("List")]
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private UIListPool<LocationRowView> _rowPool = new();
        [SerializeField] private LocationDemandWidgetView _demandWidgetPrefab;

        private Action _onScrolled;
        private Vector2 _lastScrollPosition;

        protected override void Awake()
        {
            base.Awake();
            if (_scrollRect != null)
            {
                _lastScrollPosition = _scrollRect.normalizedPosition;
                _scrollRect.onValueChanged.AddListener(OnScrollChanged);
            }

            if (_demandWidgetPrefab != null)
                WidgetRegistry.Register<LocationDemandWidgetData>(_demandWidgetPrefab);
        }

        public void Render(IReadOnlyList<LocationListItemModel> models, Action<string> onStart, Action<string> onUnlock,
            Action<string, RectTransform> onDemandInfo, Action onScrolled, IUiSpriteProvider sprites)
        {
            _onScrolled = onScrolled;
            _rowPool.DisableAll();

            if (models != null)
            {
                for (var i = 0; i < models.Count; i++)
                {
                    var model = models[i];
                    if (model == null) continue;
                    _rowPool.GetNext().Bind(model, onStart, onUnlock, onDemandInfo, sprites);
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
            if (_scrollRect == null)
                return;

            var position = _scrollRect.normalizedPosition;
            if ((position - _lastScrollPosition).sqrMagnitude <= 0.000001f)
                return;

            _lastScrollPosition = position;
            _onScrolled?.Invoke();
        }

        protected override void OnDestroy()
        {
            if (_scrollRect != null)
                _scrollRect.onValueChanged.RemoveListener(OnScrollChanged);

            base.OnDestroy();
        }
    }
}
