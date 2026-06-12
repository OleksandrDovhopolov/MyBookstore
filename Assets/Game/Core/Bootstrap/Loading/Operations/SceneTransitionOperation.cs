using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Bootstrap.Loading
{
    public sealed class SceneTransitionOperation : LoadingOperationBase
    {
        private readonly ISceneTransitionService _service;
        private readonly string _sceneName;

        public SceneTransitionOperation(ISceneTransitionService service, string sceneName)
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
            _sceneName = sceneName;
        }

        protected override async UniTask ExecuteInternalAsync(CancellationToken ct)
        {
            await _service.TransitionToAsync(_sceneName, new Progress<float>(ReportProgress), ct);
        }
    }
}
