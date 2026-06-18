using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class FadeAnimation : WindowAnimation
    {
        [SerializeField, Min(0f)] private float _duration = 0.25f;

        private CanvasGroup _canvasGroup;

        public override float DefaultDuration => _duration;

        private CanvasGroup CanvasGroup =>
            _canvasGroup != null ? _canvasGroup : _canvasGroup = GetComponent<CanvasGroup>();

        public override UniTask PlayInAsync(CancellationToken ct) => LerpAlphaAsync(0f, 1f, ct);
        public override UniTask PlayOutAsync(CancellationToken ct) => LerpAlphaAsync(1f, 0f, ct);

        private async UniTask LerpAlphaAsync(float from, float to, CancellationToken ct)
        {
            CanvasGroup.alpha = from;

            if (_duration <= 0f)
            {
                CanvasGroup.alpha = to;
                return;
            }

            var elapsed = 0f;
            while (elapsed < _duration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / _duration);
                CanvasGroup.alpha = Mathf.Lerp(from, to, t);
                await UniTask.NextFrame(ct);
            }

            CanvasGroup.alpha = to;
        }
    }
}
