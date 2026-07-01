using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
using UnityEngine;

namespace Game.Bootstrap
{
    // Transition router populated from RegisterUiSystem's build callback after UIManagerCanvas is instantiated.
    // Runtime PlayCover/PlayReveal never resolve IUICanvasRoot, which avoids VContainer Lazy self-reference.
    internal sealed class DeferredTransitionAnimationService : ITransitionAnimationService
    {
        private ITransitionAnimationService _inner;
        private bool _warned;

        public UniTask PlayCoverAsync(CancellationToken ct) => Inner().PlayCoverAsync(ct);

        public UniTask PlayRevealAsync(CancellationToken ct) => Inner().PlayRevealAsync(ct);

        public void SetTransition(MonoBehaviour transitionAnimation)
        {
            if (transitionAnimation is ITransitionAnimationService service)
            {
                _inner = service;
                return;
            }

            WarnMissing();
            _inner = new NoOpTransitionAnimationService();
        }

        private ITransitionAnimationService Inner()
        {
            if (_inner != null) return _inner;

            WarnMissing();
            _inner = new NoOpTransitionAnimationService();

            return _inner;
        }

        private void WarnMissing()
        {
            if (_warned) return;
            _warned = true;
            Debug.LogWarning("[Transition] Transition service is missing on the UIManagerCanvas prefab — " +
                             "using a no-op cover. Assign a MonoBehaviour implementing " +
                             "ITransitionAnimationService on UICanvasRoot.");
        }
    }
}
