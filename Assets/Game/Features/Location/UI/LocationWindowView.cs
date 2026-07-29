using System;
using System.Collections.Generic;
using Game.UI;
using SpriteService;
using UIShared;
using UnityEngine;

namespace Game.Location.UI
{
    public sealed class LocationWindowView : WindowView
    {
        [Header("List")]
        [SerializeField] private UIListPool<LocationRowView> _rowPool = new();

        public void Render(IReadOnlyList<LocationListItemModel> models, Action<string> onStart, Action<string> onUnlock,
            IUiSpriteProvider sprites)
        {
            _rowPool.DisableAll();

            if (models != null)
            {
                for (var i = 0; i < models.Count; i++)
                {
                    var model = models[i];
                    if (model == null) continue;
                    _rowPool.GetNext().Bind(model, onStart, onUnlock, sprites);
                }
            }

            _rowPool.DisableNonActive();
        }

        public void Clear() => _rowPool.DisableAll();
    }
}
