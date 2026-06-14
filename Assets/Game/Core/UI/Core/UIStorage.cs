using System;
using System.Collections.Generic;

namespace Game.UI
{
    public sealed class UIStorage : IUIStorage
    {
        private readonly Dictionary<(Type type, int primaryKey), IWindowController> _byKey = new();
        private readonly Dictionary<IWindowController, (Type type, int primaryKey)> _reverse = new();

        public IReadOnlyCollection<IWindowController> All => _reverse.Keys;

        public bool TryGet(Type controllerType, int primaryKey, out IWindowController controller)
            => _byKey.TryGetValue((controllerType, primaryKey), out controller);

        public void Add(IWindowController controller, int primaryKey)
        {
            var key = (controller.GetType(), primaryKey);
            _byKey[key] = controller;
            _reverse[controller] = key;
        }

        public bool Remove(IWindowController controller)
        {
            if (!_reverse.Remove(controller, out var key)) return false;
            _byKey.Remove(key);
            return true;
        }

        public void Clear()
        {
            _byKey.Clear();
            _reverse.Clear();
        }
    }
}
