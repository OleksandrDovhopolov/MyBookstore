using System.Collections.Generic;
using SpriteService;
using UIShared;
using UnityEngine;

namespace Game.Journal.UI
{
    public sealed class JournalPeoplePageView : MonoBehaviour
    {
        [SerializeField] private UIListPool<JournalCharacterRowView> _rowPool = new();

        public void Render(IReadOnlyList<JournalCharacterItemModel> models, IUiSpriteProvider sprites)
        {
            _rowPool.DisableAll();

            if (models != null)
            {
                for (var i = 0; i < models.Count; i++)
                {
                    var model = models[i];
                    if (model == null || model.Locked) continue;
                    _rowPool.GetNext().Bind(model, sprites);
                }
            }

            _rowPool.DisableNonActive();
        }

        public void Clear() => _rowPool.DisableAll();
    }
}
