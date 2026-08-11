using System;
using SpriteService;
using UIShared;
using UnityEngine;

namespace Game.Journal.UI
{
    public sealed class JournalObjectsPageView : MonoBehaviour
    {
        [SerializeField] private UIListPool<JournalObjectCardView> _objectPool = new();
        [SerializeField] private UIListPool<JournalBonusRowView> _bonusPool = new();

        public void Render(JournalObjectsViewModel model, IUiSpriteProvider sprites, Action<string> onInfoClicked)
        {
            _objectPool.DisableAll();
            _bonusPool.DisableAll();

            var objects = model?.Objects;
            if (objects != null)
            {
                for (var i = 0; i < objects.Count; i++)
                {
                    var item = objects[i];
                    if (item == null) continue;
                    _objectPool.GetNext().Bind(item, sprites, onInfoClicked);
                }
            }

            var bonuses = model?.Bonuses;
            if (bonuses != null)
            {
                Debug.Log($"[Journal.Objects] bonuses count={bonuses.Count}");
                for (var i = 0; i < bonuses.Count; i++)
                {
                    var item = bonuses[i];
                    if (item == null) continue;
                    Debug.Log(
                        $"[Journal.Objects] bonus[{i}] label='{item.Label}' percent='{item.PercentText}' " +
                        $"positive={item.IsPositive} iconKey='{item.IconKey ?? "<null>"}'");
                    _bonusPool.GetNext().Bind(item, sprites);
                }
            }

            _objectPool.DisableNonActive();
            _bonusPool.DisableNonActive();
        }

        public void Clear()
        {
            _objectPool.DisableAll();
            _bonusPool.DisableAll();
        }
    }
}
