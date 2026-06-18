using System.Collections.Generic;

namespace Game.UI
{
    public sealed class UIStack : IUIStack
    {
        // HUD is intentionally omitted: HideTopAsync() without an explicit layer asks
        // GetFocused() for the topmost focusable window, and HUD must never be focusable.
        private static readonly WindowLayer[] FocusOrder =
        {
            WindowLayer.System,
            WindowLayer.Develop,
            WindowLayer.Additional,
            WindowLayer.Main,
        };

        private readonly Dictionary<WindowLayer, List<IWindowController>> _byLayer = new()
        {
            [WindowLayer.Hud] = new List<IWindowController>(),
            [WindowLayer.Main] = new List<IWindowController>(),
            [WindowLayer.Additional] = new List<IWindowController>(),
            [WindowLayer.System] = new List<IWindowController>(),
            [WindowLayer.Develop] = new List<IWindowController>(),
        };

        private readonly Dictionary<IWindowController, WindowLayer> _layerOf = new();

        public void Push(IWindowController controller, WindowLayer layer)
        {
            if (_layerOf.TryGetValue(controller, out var existingLayer))
            {
                _byLayer[existingLayer].Remove(controller);
            }

            _byLayer[layer].Add(controller);
            _layerOf[controller] = layer;
        }

        public bool Remove(IWindowController controller)
        {
            if (!_layerOf.Remove(controller, out var layer)) return false;
            _byLayer[layer].Remove(controller);
            return true;
        }

        public IWindowController GetTop(WindowLayer layer)
        {
            var list = _byLayer[layer];
            return list.Count == 0 ? null : list[^1];
        }

        public IWindowController GetFocused()
        {
            foreach (var layer in FocusOrder)
            {
                var top = GetTop(layer);
                if (top != null) return top;
            }
            return null;
        }

        public WindowLayer? GetLayerOf(IWindowController controller)
            => _layerOf.TryGetValue(controller, out var layer) ? layer : null;

        public IReadOnlyList<IWindowController> GetAll(WindowLayer layer) => _byLayer[layer];
    }
}
