using System;
using System.Collections.Generic;
using Game.Quest.UI;
using SpriteService;
using UIShared;
using UnityEngine;

namespace Game.Journal.UI
{
    public sealed class JournalQuestsPageView : MonoBehaviour
    {
        [SerializeField] private UIListPool<QuestRowView> _rowPool = new();

        public void Render(
            IReadOnlyList<QuestItemModel> models,
            Action<string> onClaim,
            Action<QuestRewardItemModel, RectTransform> onRewardInfo,
            IUiSpriteProvider sprites)
        {
            _rowPool.DisableAll();

            if (models != null)
            {
                for (var i = 0; i < models.Count; i++)
                {
                    var model = models[i];
                    if (model == null) continue;
                    _rowPool.GetNext().Bind(model, onClaim, onRewardInfo, sprites);
                }
            }

            _rowPool.DisableNonActive();
        }

        public void Clear() => _rowPool.DisableAll();
    }
}
