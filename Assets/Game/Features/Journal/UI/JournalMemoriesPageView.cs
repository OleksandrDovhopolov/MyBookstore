using System.Collections.Generic;
using SpriteService;
using UIShared;
using UnityEngine;

namespace Game.Journal.UI
{
    public sealed class JournalMemoriesPageView : MonoBehaviour
    {
        [SerializeField] private UIListPool<JournalMemoryRowView> _rowPool = new();

        public void Render(IReadOnlyList<JournalMemoryItemModel> models, IUiSpriteProvider sprites)
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
