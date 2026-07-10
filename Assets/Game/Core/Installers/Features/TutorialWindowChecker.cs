using System;
using System.Collections.Generic;
using Game.DayCycle.Results.UI;
using Game.Location.UI;
using Game.Preparation.UI;
using Game.Tutorial.Steps;
using Game.UI;

namespace Game.Bootstrap
{
    // Concrete window-id → IsShown map for awaitWindow steps. Lives in the bootstrap layer (which already
    // references the window features) so Game.Tutorial stays decoupled from Game.Preparation / Game.Location.
    public sealed class TutorialWindowChecker : ITutorialWindowChecker
    {
        private readonly Dictionary<string, Func<bool>> _byId;

        public TutorialWindowChecker(IUIManager ui)
        {
            _byId = new Dictionary<string, Func<bool>>(StringComparer.OrdinalIgnoreCase)
            {
                ["preparation"] = ui.IsWindowShown<PreparationWindow>,
                ["location"] = ui.IsWindowShown<LocationWindow>,
                ["results"] = ui.IsWindowShown<ResultsWindow>,
            };
        }

        public bool TryGetShown(string windowId, out bool shown)
        {
            shown = false;
            if (windowId == null || !_byId.TryGetValue(windowId, out var check)) return false;
            shown = check();
            return true;
        }
    }
}
