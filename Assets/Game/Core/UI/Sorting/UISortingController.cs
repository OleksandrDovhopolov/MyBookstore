using System.Collections.Generic;
using UnityEngine;

namespace Game.UI
{
    public sealed class UISortingController : IUISortingController
    {
        // Tracks the highest assigned sortingOrder per layer. Starts at the layer's base value
        // and bumps by OrderStep on each Apply. Compacted back down when the top window closes.
        private readonly Dictionary<WindowLayer, int> _topOrderByLayer = new()
        {
            [WindowLayer.Hud] = UISortingLayers.HudBase,
            [WindowLayer.Main] = UISortingLayers.MainBase,
            [WindowLayer.Additional] = UISortingLayers.AdditionalBase,
            [WindowLayer.System] = UISortingLayers.SystemBase,
            [WindowLayer.Develop] = UISortingLayers.DevelopBase,
        };

        private readonly Dictionary<IWindowController, (WindowLayer layer, int order)> _assigned = new();

        public void Apply(IWindowController controller, WindowLayer layer)
        {
            if (controller?.View?.Canvas == null)
            {
                Debug.LogWarning($"[UISortingController] Controller {controller?.GetType().Name} has no Canvas on view.");
                return;
            }

            _topOrderByLayer[layer] += UISortingLayers.OrderStep;
            var order = _topOrderByLayer[layer];

            var canvas = controller.View.Canvas;
            canvas.overrideSorting = true;
            canvas.sortingOrder = order;

            _assigned[controller] = (layer, order);
        }

        public void Release(IWindowController controller)
        {
            if (controller == null) return;
            if (!_assigned.Remove(controller, out var prev)) return;

            // Compact: if this was the top order in its layer, roll back the counter.
            var baseOrder = UISortingLayers.BaseOrderFor(prev.layer);
            if (_topOrderByLayer[prev.layer] == prev.order)
            {
                _topOrderByLayer[prev.layer] -= UISortingLayers.OrderStep;
                if (_topOrderByLayer[prev.layer] < baseOrder)
                    _topOrderByLayer[prev.layer] = baseOrder;
            }
        }
    }
}
