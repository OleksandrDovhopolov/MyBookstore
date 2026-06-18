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
        void Dispose();
    }
}
