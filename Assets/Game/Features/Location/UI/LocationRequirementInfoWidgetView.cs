using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI.ContentWidget;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Location.UI
{
    public sealed class LocationRequirementInfoWidgetView : MonoBehaviour, IContentWidgetView
    {
        [SerializeField] private TextMeshProUGUI _titleLabel;
        [SerializeField] private TextMeshProUGUI _hintLabel;
        [SerializeField] private Button _closeButton;

        private void Awake()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(OnCloseClicked);
        }

        public bool Setup(ContentWidgetDataBase data)
        {
            if (data is not LocationRequirementInfoWidgetData info)
                return false;

            if (_titleLabel != null)
                _titleLabel.text = info.Title;
            if (_hintLabel != null)
                _hintLabel.text = info.HintText;

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
