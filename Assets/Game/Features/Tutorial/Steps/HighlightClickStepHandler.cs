using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Game.Tutorial.Presentation;
using Infrastructure.TutorialUI;
using UnityEngine;

namespace Game.Tutorial.Steps
{
    /// <summary>
    /// Dim with a hole over the target + optional pointer; advances when the real target button is clicked
    /// through the hole. NO UIManager lock — the blackout slices block everything outside the hole, and the
    /// hole passes the click to the button beneath. Missing target → warn + auto-advance (never soft-lock).
    /// </summary>
    public sealed class HighlightClickStepHandler : ITutorialStepHandler
    {
        private const string LogPrefix = "[Tutorial]";

        private readonly TutorialOverlayController _overlay;
        private readonly ITutorialTargetRegistry _targets;

        public HighlightClickStepHandler(TutorialOverlayController overlay, ITutorialTargetRegistry targets)
        {
            _overlay = overlay;
            _targets = targets;
        }

        public string Type => TutorialStepTypes.HighlightClick;

        public async UniTask ExecuteAsync(TutorialStepConfig step, CancellationToken ct)
        {
            if (!_targets.TryGetTarget(step?.Target, out var rect))
            {
                Debug.LogWarning($"{LogPrefix} highlight target '{step?.Target}' not registered; auto-advancing.");
                return;
            }

            try
            {
                await _overlay.HighlightAndWaitClickAsync(rect, step?.Text, step?.Placement, step?.Pointer ?? false, ct);
            }
            finally
            {
                _overlay.HideHighlight();
            }
        }
    }
}
