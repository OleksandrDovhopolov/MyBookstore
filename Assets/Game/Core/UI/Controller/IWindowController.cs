using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.UI
{
    public interface IWindowController
    {
        WindowAttribute Attribute { get; }
        WindowArgs Arguments { get; }
        IWindow View { get; }
        bool IsShown { get; }
        bool IsCloseBlocked { get; }

        event Action<IWindowController> Closed;

        void Configure(WindowView view, WindowAttribute attribute);
        void ApplyArguments(WindowArgs args);
        UniTask ShowAsync(CancellationToken ct);
        UniTask HideAsync(bool isClosed, CancellationToken ct);

        /// <summary>
        /// Toggles the window's visual presence via its <see cref="IWindow.CanvasGroup"/> (alpha +
        /// interactable + raycast blocking) without going through the show/hide lifecycle. Used to keep
        /// a window mounted (and loading) while it must stay invisible — e.g. the hub HUD behind the
        /// first-entry welcome letter.
        /// </summary>
        void SetHudVisible(bool visible);

        void Dispose();
    }
}
