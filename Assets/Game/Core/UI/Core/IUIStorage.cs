using System;
using System.Collections.Generic;

namespace Game.UI
{
    public interface IUIStorage
    {
        bool TryGet(Type controllerType, int primaryKey, out IWindowController controller);
        void Add(IWindowController controller, int primaryKey);
        bool Remove(IWindowController controller);
        IReadOnlyCollection<IWindowController> All { get; }
        void Clear();
    }
}
