using SpriteService;
using UIShared;
using UnityEngine;

namespace Game.Journal.UI
{
    public sealed class JournalObjectsPageView : MonoBehaviour
    {
        [SerializeField] private UIListPool<JournalObjectCardView> _objectPool = new();
        [SerializeField] private UIListPool<JournalBonusRowView> _bonusPool = new();

        public void Render(JournalObjectsViewModel model, IUiSpriteProvider sprites)
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
                    _objectPool.GetNext().Bind(item, sprites);
                }
            }

            var bonuses = model?.Bonuses;
            if (bonuses != null)
            {
                for (var i = 0; i < bonuses.Count; i++)
                {
                    var item = bonuses[i];
                    if (item == null) continue;
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
