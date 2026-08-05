using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI.ContentWidget;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Shop.UI
{
    public sealed class ShopItemWidgetView : MonoBehaviour, IContentWidgetView
    {
        [SerializeField] private TMP_Text _descriptionLabel;
        [SerializeField] private Button _closeButton;

        private void Awake()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(OnCloseClicked);
        }

        public bool Setup(ContentWidgetDataBase data)
        {
            if (data is not ShopItemWidgetData item)
                return false;

            if (_descriptionLabel != null)
                _descriptionLabel.text = item.Description ?? string.Empty;

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
