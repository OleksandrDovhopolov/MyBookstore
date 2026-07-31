using System;
using System.Collections.Generic;
using Game.UI;
using Game.UI.ContentWidget;
using SpriteService;
using UIShared;
using UnityEngine;

namespace Game.Location.UI
{
    public sealed class LocationWindowView : WindowView
    {
        [Header("List")]
        [SerializeField] private UIListPool<LocationRowView> _rowPool = new();
        [SerializeField] private LocationDemandWidgetView _demandWidgetPrefab;

        protected override void Awake()
        {
            base.Awake();
            if (_demandWidgetPrefab != null)
                WidgetRegistry.Register<LocationDemandWidgetData>(_demandWidgetPrefab);
        }

        public void Render(IReadOnlyList<LocationListItemModel> models, Action<string> onStart, Action<string> onUnlock,
            Action<string, RectTransform> onDemandInfo, IUiSpriteProvider sprites)
        {
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

        public void Clear() => _rowPool.DisableAll();
    }
}
