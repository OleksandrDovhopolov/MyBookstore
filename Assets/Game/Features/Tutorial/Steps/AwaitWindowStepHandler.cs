using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using UnityEngine;

namespace Game.Tutorial.Steps
{
    /// <summary>
    /// Waits until a named window (see <see cref="ITutorialWindowChecker"/>) is shown, by polling ~4 Hz.
    /// Unknown window id → warn + auto-advance (never wait forever).
    /// </summary>
    public sealed class AwaitWindowStepHandler : ITutorialStepHandler
    {
        private const string LogPrefix = "[Tutorial]";
        private const int PollMs = 250;

        private readonly ITutorialWindowChecker _checker;

        public AwaitWindowStepHandler(ITutorialWindowChecker checker) => _checker = checker;

        public string Type => TutorialStepTypes.AwaitWindow;

        public async UniTask ExecuteAsync(TutorialStepConfig step, CancellationToken ct)
        {
            var windowId = step?.Window;

            if (_checker == null || !_checker.TryGetShown(windowId, out var shown))
            {
                Debug.LogWarning($"{LogPrefix} awaitWindow: unknown window id '{windowId}'; auto-advancing.");
                return;
            }

            while (!shown)
            {
                await UniTask.Delay(PollMs, cancellationToken: ct);
                _checker.TryGetShown(windowId, out shown);
            }
        }
    }
}
