using System;
using System.Collections.Generic;
using Game.UI;
using SpriteService;
using UIShared;
using UnityEngine;

namespace Game.Quest.UI
{
    /// <summary>Renders the active-quests list via a pooled row view. Mirrors <c>JournalWindowView</c>.</summary>
    public sealed class QuestWindowView : WindowView
    {
        [Header("List")]
        [Tooltip("Row prefab + content parent are assigned on the pool in the inspector.")]
        [SerializeField] private UIListPool<QuestRowView> _questPool = new();

        public void Render(IReadOnlyList<QuestItemModel> models, Action<string> onClaim, IUiSpriteProvider sprites)
        {
            _questPool.DisableAll();

            if (models != null)
            {
                for (var i = 0; i < models.Count; i++)
                {
                    var model = models[i];
                    if (model == null) continue;
                    _questPool.GetNext().Bind(model, onClaim, sprites);
                }
            }

            _questPool.DisableNonActive();
        }

        public void Clear() => _questPool.DisableAll();
    }
}
