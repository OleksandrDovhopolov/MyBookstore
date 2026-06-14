using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.UI
{
    public static class WindowControllerExtensions
    {
        public static UniTask WaitForCloseAsync(this IWindowController controller, CancellationToken ct = default)
        {
            if (controller == null) return UniTask.CompletedTask;

            var tcs = new UniTaskCompletionSource();

            void OnClosed(IWindowController _)
            {
                controller.Closed -= OnClosed;
                tcs.TrySetResult();
            }

            controller.Closed += OnClosed;

            if (ct.CanBeCanceled)
            {
                ct.Register(() =>
                {
                    controller.Closed -= OnClosed;
                    tcs.TrySetCanceled(ct);
                });
            }

            return tcs.Task;
        }
    }
}
