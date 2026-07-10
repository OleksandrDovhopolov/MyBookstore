using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Bootstrap.Loading
{
    public sealed class SceneTransitionOperation : LoadingOperationBase
    {
        private readonly ISceneTransitionService _service;
        private readonly ITransitionAnimationService _animation;
        private readonly string _sceneName;

        public SceneTransitionOperation(
            ISceneTransitionService service, ITransitionAnimationService animation, string sceneName)
            : base(
                id: "scene_transition",
                description: "Loading scene",
                isCritical: true,
                weight: 0.15f,
                displayPriority: 60,
                retryPolicy: new LoadingRetryPolicy(1, TimeSpan.Zero),
                timeout: TimeSpan.FromSeconds(15))
        {
            _service = service;
            _animation = animation;
            _sceneName = sceneName;
        }

        protected override async UniTask ExecuteInternalAsync(CancellationToken ct)
        {
            // Raise the transition cover before swapping scenes; reveal happens in MainSceneBootstrap
            // once GameplayScene is data-ready. The cover lives on the DontDestroyOnLoad UIManagerCanvas.
            if (_animation != null)
                await _animation.PlayCoverAsync(ct);

            await _service.TransitionToAsync(_sceneName, new Progress<float>(ReportProgress), ct);
        }
    }
}
