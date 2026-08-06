using System.Collections.Generic;
using SpriteService;
using UIShared;
using UnityEngine;

namespace Game.Journal.UI
{
    public sealed class JournalMemoriesPageView : MonoBehaviour
    {
        private const string HardcodedMemoryTitle = "New beginning";
        private const string HardcodedMemoryDescription =
            "Leaving my settled place is the best decision of my thousand-year life. Who said a deity can't choose its home?";

        [SerializeField] private UIListPool<JournalMemoryRowView> _rowPool = new();

        public void Render(IReadOnlyList<JournalMemoryItemModel> models, IUiSpriteProvider sprites)
        {
            _rowPool.DisableAll();

            if (models != null)
            {
                var visibleIndex = 0;
                for (var i = 0; i < models.Count; i++)
                {
                    var model = models[i];
                    if (model == null) continue;
                    _rowPool.GetNext().Bind(WithHardcodedText(model), sprites, visibleIndex % 2 == 0);
                    visibleIndex++;
                }
            }

            _rowPool.DisableNonActive();
        }

        public void Clear() => _rowPool.DisableAll();

        private static JournalMemoryItemModel WithHardcodedText(JournalMemoryItemModel model) =>
            new(
                model.CharacterId,
                model.MemoryId,
                HardcodedMemoryTitle,
                HardcodedMemoryDescription,
                model.PhotoKey,
                model.Order,
                model.IsUnlocked,
                model.IsGolden,
                model.LinkedQuestState);
    }
}
