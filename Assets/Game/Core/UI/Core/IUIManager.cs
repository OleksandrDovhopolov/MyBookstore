using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.UI
{
    public interface IUIManager
    {
        /// <summary>
        /// Raised after a window has actually been shown (post <see cref="IWindowController.ShowAsync"/>).
        /// Handlers must NOT synchronously await back into the manager — it fires while the internal
        /// show gate is held, so re-entrant Hide/Show calls have to be fire-and-forget.
        /// </summary>
        event Action<IWindowController> WindowShown;

        UniTask<T> ShowAsync<T>(WindowArgs args = null, CancellationToken ct = default)
            where T : class, IWindowController, new();

        UniTask HideAsync<T>(bool forceClose = false, CancellationToken ct = default)
            where T : class, IWindowController;

        UniTask HideAsync(IWindowController controller, bool forceClose = false, CancellationToken ct = default);

        UniTask HideTopAsync(WindowLayer? layer = null, CancellationToken ct = default);

        IWindowController GetTopWindow(WindowLayer? layer = null);

        bool IsWindowShown<T>() where T : class, IWindowController;
        bool IsWindowSpawned<T>() where T : class, IWindowController;

        Lock SetManualLock(object owner);
    }
}
