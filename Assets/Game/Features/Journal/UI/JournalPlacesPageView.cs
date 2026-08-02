using System.Collections.Generic;
using SpriteService;
using UIShared;
using UnityEngine;

namespace Game.Journal.UI
{
    public sealed class JournalPlacesPageView : MonoBehaviour
    {
        [SerializeField] private UIListPool<JournalPlaceRowView> _rowPool = new();

        public void Render(IReadOnlyList<JournalPlaceItemModel> models, IUiSpriteProvider sprites)
        {
            _rowPool.DisableAll();

            if (models != null)
            {
                for (var i = 0; i < models.Count; i++)
                {
                    var model = models[i];
                    if (model == null) continue;
                    _rowPool.GetNext().Bind(model, sprites);
                }
            }

            _rowPool.DisableNonActive();
        }

        public void Clear() => _rowPool.DisableAll();
    }
}
