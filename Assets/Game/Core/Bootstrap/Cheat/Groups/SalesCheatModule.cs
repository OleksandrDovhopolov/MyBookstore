using Book.Sell.Services;
using Book.Sell.UI;
using cheatModule;
using UnityEngine;

namespace Game.Cheat
{
    /// <summary>
    /// Debug control over the live sales day. The <see cref="ISalesDayController"/> lives in the gameplay
    /// scope and is not resolvable from the global-scope cheat panel (see CheatModuleView), so it is reached
    /// through the active <see cref="SalesScreenView"/> in the scene. Buttons no-op with a warning when no
    /// sales day is running.
    /// </summary>
    public sealed class SalesCheatModule : ICheatsModule
    {
        private const string CardsGroup = "Sales Day";
        private const string LogTag = "[SalesCheat]";

        public void Initialize(ICheatsContainer cheatsContainer)
        {
            cheatsContainer.AddItem<CheatButtonItem>(item =>
                item.OnClick("End Day", () => ForceCompleteDay(zeroOut: false))
                    .WithGroup(CardsGroup));

            cheatsContainer.AddItem<CheatButtonItem>(item =>
                item.OnClick("End Day (zero result)", () => ForceCompleteDay(zeroOut: true))
                    .WithGroup(CardsGroup));
        }

        private static void ForceCompleteDay(bool zeroOut)
        {
            var controller = ResolveController();
            if (controller == null)
            {
                Debug.LogWarning($"{LogTag} No active sales day — enter a location and start the day first.");
                return;
            }

            if (controller.IsDayCompleted)
            {
                Debug.LogWarning($"{LogTag} The sales day is already completed.");
                return;
            }

            Debug.Log($"{LogTag} Force-completing the sales day (zeroOut={zeroOut}).");
            controller.ForceCompleteDay(zeroOut);
        }

        // The controller is not DI-reachable from the global cheat scope; find the active screen view that
        // owns it. Active-only (default): a torn-down screen from a prior scene must not be picked up.
        private static ISalesDayController ResolveController()
        {
            var view = Object.FindAnyObjectByType<SalesScreenView>();
            return view != null ? view.Controller : null;
        }
    }
}
