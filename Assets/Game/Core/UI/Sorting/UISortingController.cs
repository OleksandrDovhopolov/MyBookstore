using System.Collections.Generic;
using UnityEngine;

namespace Game.UI
{
    public sealed class UISortingController : IUISortingController
    {
        private readonly Dictionary<WindowLayer, int> _topOrderByLayer = new()
        {
            [WindowLayer.Main] = 0,
            [WindowLayer.Additional] = 0,
            [WindowLayer.System] = 0,
            [WindowLayer.Develop] = 0,
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
            canvas.sortingLayerName = UISortingLayers.For(layer);
            canvas.sortingOrder = order;

            _assigned[controller] = (layer, order);
        }

        public void Release(IWindowController controller)
        {
            if (controller == null) return;
            if (!_assigned.Remove(controller, out var prev)) return;

            // Compact: if this was the top order in its layer, roll back the counter.
            if (_topOrderByLayer[prev.layer] == prev.order)
            {
                _topOrderByLayer[prev.layer] -= UISortingLayers.OrderStep;
                if (_topOrderByLayer[prev.layer] < 0)
                    _topOrderByLayer[prev.layer] = 0;
            }
        }
    }
}
