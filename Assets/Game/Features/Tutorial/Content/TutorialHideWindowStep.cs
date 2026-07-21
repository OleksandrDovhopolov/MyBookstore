using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Tutorial.API;
using Game.UI;

namespace Game.Tutorial.Content
{
    public sealed class TutorialHideWindowStep<TWindow> : ITutorialStep
        where TWindow : class, IWindowController
    {
        private readonly TutorialAsyncActionStep _inner;

        public TutorialHideWindowStep(string id, IUIManager ui, bool forceClose = true)
        {
            _inner = new TutorialAsyncActionStep(id, ct => HideWindowAsync(ui, forceClose, ct));
        }

        public string Id => _inner.Id;

        public UniTask ExecuteAsync(CancellationToken ct)
            => _inner.ExecuteAsync(ct);

        private static async UniTask HideWindowAsync(IUIManager ui, bool forceClose, CancellationToken ct)
        {
            if (ui == null || !ui.IsWindowShown<TWindow>())
                return;

            await ui.HideAsync<TWindow>(forceClose, ct);
        }
    }
}
