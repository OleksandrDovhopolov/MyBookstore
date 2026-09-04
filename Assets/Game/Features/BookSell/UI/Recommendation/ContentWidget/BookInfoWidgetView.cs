using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI.ContentWidget;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Book.Sell.UI
{
    public sealed class BookInfoWidgetView : MonoBehaviour, IContentWidgetView, IPointerClickHandler
    {
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _genresLabel;
        [SerializeField] private TMP_Text _qualitiesLabel;

        public bool Setup(ContentWidgetDataBase data)
        {
            if (data is not BookInfoWidgetData book)
                return false;

            Set(_titleLabel, book.Title);
            Set(_genresLabel, $"Genres: {book.Genres}");
            Set(_qualitiesLabel, $"Qualities: {book.Qualities}");
            return true;
        }

        public async UniTask OnViewCreatedAsync(CancellationToken ct)
        {
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, ct);

            if (transform is RectTransform rect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                Canvas.ForceUpdateCanvases();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            GetComponentInParent<ContentWidgetView>()?.RequestClose();
        }

        private static void Set(TMP_Text label, string value)
        {
            if (label != null) label.text = value ?? string.Empty;
        }
    }
}
