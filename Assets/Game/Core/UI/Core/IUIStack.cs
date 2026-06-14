using System.Collections.Generic;

namespace Game.UI
{
    public interface IUIStack
    {
        void Push(IWindowController controller, WindowLayer layer);
        bool Remove(IWindowController controller);
        IWindowController GetTop(WindowLayer layer);
        IWindowController GetFocused();
        WindowLayer? GetLayerOf(IWindowController controller);
        IReadOnlyList<IWindowController> GetAll(WindowLayer layer);
    }
}
