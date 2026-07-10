using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
using Game.Configs.Models;

namespace Game.Tutorial.Steps
{
    /// <summary>
    /// Waits until the player is inside a location. Checks <see cref="IGameFlowService.IsLocationLoaded"/>
    /// first (so a resume already in-location does not hang), then subscribes to LocationLoadedChanged.
    /// </summary>
    public sealed class AwaitLocationStepHandler : ITutorialStepHandler
    {
        private readonly IGameFlowService _gameFlow;

        public AwaitLocationStepHandler(IGameFlowService gameFlow) => _gameFlow = gameFlow;

        public string Type => TutorialStepTypes.AwaitLocation;

        public async UniTask ExecuteAsync(TutorialStepConfig step, CancellationToken ct)
        {
            if (_gameFlow == null || _gameFlow.IsLocationLoaded) return;

            var tcs = new UniTaskCompletionSource();
            void OnChanged(bool loaded)
            {
                if (loaded) tcs.TrySetResult();
            }

            _gameFlow.LocationLoadedChanged += OnChanged;
            try
            {
                if (_gameFlow.IsLocationLoaded) return;
                await tcs.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                _gameFlow.LocationLoadedChanged -= OnChanged;
            }
        }
    }
}
