using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI;
using UnityEngine;

namespace Infrastructure
{
    public class WidgetInfoAnimation : WindowAnimation
    {
        [SerializeField] private float _showDuration;
        [SerializeField] private float _hideDuration;

        private CanvasGroup _canvasGroup;

        public override float DefaultDuration => _showDuration;
        
        public override async UniTask PlayInAsync(CancellationToken ct)
        {
            CanvasGroup.alpha = 1f;
            await UniTask.WaitForSeconds(_showDuration, cancellationToken: ct);
        }

        public override async UniTask PlayOutAsync(CancellationToken ct)
        {
            await UniTask.WaitForSeconds(_hideDuration, cancellationToken: ct);
            CanvasGroup.alpha = 0f;
        }

        private CanvasGroup CanvasGroup =>
            _canvasGroup != null ? _canvasGroup : _canvasGroup = GetComponent<CanvasGroup>();
    }
}
