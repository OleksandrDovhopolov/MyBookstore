using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UIShared;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Window animation that drives a set of <see cref="AnimatedShowHidePanel"/> — e.g. a top and a
    /// bottom panel that slide/fade in on open and out on close. On PlayIn every panel <c>Show</c>s;
    /// on PlayOut every panel <c>Hide</c>s. Panels are snapped to Hidden instantly at the start of
    /// PlayIn, so they always animate in from off-screen regardless of their authored state — see
    /// the note on <see cref="PlayInAsync"/>.
    /// </summary>
    public sealed class PanelsWindowAnimation : WindowAnimation
    {
        [Tooltip("Window root CanvasGroup; toggled to 1/0 around the panel animation. Optional.")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [Tooltip("Panels to drive, e.g. top and bottom.")]
        [SerializeField] private AnimatedShowHidePanel[] _panels;
        [SerializeField, Min(0f)] private float _defaultDuration = 0.25f;

        public override float DefaultDuration => _defaultDuration;

        // Snap hidden BEFORE revealing the root so panels always play their in-animation, even if a
        // panel is authored as visible in the prefab (_startShown = true). Because this runs
        // synchronously before the first awaited frame, no "already shown" flash reaches the screen.
        public override async UniTask PlayInAsync(CancellationToken ct)
        {
            SnapHidden();
            if (_canvasGroup != null) _canvasGroup.alpha = 1f;
            await RunAsync(show: true, ct);
        }

        public override async UniTask PlayOutAsync(CancellationToken ct)
        {
            await RunAsync(show: false, ct);
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        }

        private void SnapHidden()
        {
            if (_panels == null) return;
            foreach (var panel in _panels)
                if (panel != null) panel.Hide(instant: true);
        }

        private async UniTask RunAsync(bool show, CancellationToken ct)
        {
            if (_panels == null || _panels.Length == 0) return;

            var tasks = new List<UniTask>(_panels.Length);
            foreach (var panel in _panels)
            {
                if (panel == null) continue;
                tasks.Add(PlayPanelAsync(panel, show, ct));
            }
            await UniTask.WhenAll(tasks);
        }

        private static async UniTask PlayPanelAsync(AnimatedShowHidePanel panel, bool show, CancellationToken ct)
        {
            var tcs = new UniTaskCompletionSource();
            if (show) panel.Show(instant: false, () => tcs.TrySetResult());
            else panel.Hide(instant: false, () => tcs.TrySetResult());

            // Complete (don't throw) if the flow is cancelled mid-animation, so window teardown
            // never hangs on the await; the panel kills its own tween in OnDestroy.
            using (ct.Register(() => tcs.TrySetResult()))
                await tcs.Task;
        }
    }
}
