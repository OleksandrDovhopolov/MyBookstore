using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI.ContentWidget;
using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Location.UI
{
    public sealed class LocationDemandWidgetView : MonoBehaviour, IContentWidgetView
    {
        [SerializeField] private TextMeshProUGUI _titleLabel;
        [SerializeField] private UIListPool<LocationDemandGenreItemView> _genrePool = new();
        [SerializeField] private Button _closeButton;

        private void Awake()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(OnCloseClicked);
        }

        public bool Setup(ContentWidgetDataBase data)
        {
            _genrePool.DisableAll();

            if (data is not LocationDemandWidgetData demand)
                return false;

            if (_titleLabel != null)
                _titleLabel.text = demand.LocationDisplayName;

            for (var i = 0; i < demand.Genres.Count; i++)
            {
                var genre = demand.Genres[i];
                _genrePool.GetNext().Bind(genre.Genre, genre.Icon);
            }

            _genrePool.DisableNonActive();
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

        private void OnCloseClicked()
        {
            GetComponentInParent<ContentWidgetView>()?.RequestClose();
        }

        private void OnDestroy()
        {
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(OnCloseClicked);
        }
    }
}
