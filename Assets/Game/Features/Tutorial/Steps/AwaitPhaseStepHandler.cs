using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Game.DayCycle.Day;
using UnityEngine;

namespace Game.Tutorial.Steps
{
    /// <summary>
    /// Waits until the day reaches a target phase. Checks the current phase FIRST (so a resume where the phase
    /// already holds does not hang), then subscribes to PhaseChanged. No overlay — passive wait.
    /// </summary>
    public sealed class AwaitPhaseStepHandler : ITutorialStepHandler
    {
        private const string LogPrefix = "[Tutorial]";

        private readonly IDayProgressService _dayProgress;

        public AwaitPhaseStepHandler(IDayProgressService dayProgress) => _dayProgress = dayProgress;

        public string Type => TutorialStepTypes.AwaitPhase;

        public async UniTask ExecuteAsync(TutorialStepConfig step, CancellationToken ct)
        {
            if (!Enum.TryParse<DayPhase>(step?.Phase, ignoreCase: true, out var target))
            {
                Debug.LogWarning($"{LogPrefix} awaitPhase: unknown phase '{step?.Phase}'; auto-advancing.");
                return;
            }

            if (_dayProgress?.Current?.CurrentPhase == target) return;

            var tcs = new UniTaskCompletionSource();
            void OnChanged(DayProgressState state)
            {
                if (state?.CurrentPhase == target) tcs.TrySetResult();
            }

            _dayProgress.PhaseChanged += OnChanged;
            try
            {
                // Re-check after subscribing to close the gap between the initial read and the subscription.
                if (_dayProgress.Current?.CurrentPhase == target) return;
                await tcs.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                _dayProgress.PhaseChanged -= OnChanged;
            }
        }
    }
}
