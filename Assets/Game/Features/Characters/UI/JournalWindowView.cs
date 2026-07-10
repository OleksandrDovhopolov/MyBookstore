using System.Collections.Generic;
using Game.Newspaper.UI;
using Game.UI;
using UIShared;
using UnityEngine;

namespace Game.Characters.UI
{
    /// <summary>Renders the Journal Characters list via a pooled row view. Mirrors LocationWindowView.</summary>
    public sealed class JournalWindowView : WindowView
    {
        [Header("List")]
        [SerializeField] private UIListPool<JournalCharacterRowView> _rowPool = new();

        public void Render(IReadOnlyList<JournalCharacterItemModel> models, IUiSpriteProvider sprites)
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
