using Book.Sell.Services;
using Book.Sell.UI;
using cheatModule;
using UnityEngine;

namespace Game.Cheat
{
    /// <summary>
    /// Cheat module for the Sales day. Lets the developer force-end the current day either keeping
    /// what the player accumulated so far or zeroing the result out entirely. The controller is
    /// looked up via the active <see cref="SalesScreenView"/> in the scene because the cheat panel
    /// lives in a global-scope UI window prefab while <see cref="ISalesDayController"/> is
    /// registered in the location scope (LocationScene) — direct VContainer resolution would fail.
    /// FindAnyObjectByType spans the additively-loaded LocationScene; returns null in the hub.
    /// </summary>
    public sealed class SalesCheatModule : ICheatsModule
    {
        private const string CardsGroup = "Sales";
        private const string LogTag = "[SalesCheat]";

        public void Initialize(ICheatsContainer cheatsContainer)
        {
            cheatsContainer.AddItem<CheatButtonItem>(item =>
                item.OnClick("Complete day (keep gold)", () => ForceComplete(zeroOut: false))
                    .WithGroup(CardsGroup));

            cheatsContainer.AddItem<CheatButtonItem>(item =>
                item.OnClick("Complete day (zero out)", () => ForceComplete(zeroOut: true))
                    .WithGroup(CardsGroup));
        }

        private static void ForceComplete(bool zeroOut)
        {
            var view = Object.FindAnyObjectByType<SalesScreenView>(FindObjectsInactive.Include);
            var controller = view != null ? view.Controller : null;

            if (controller == null)
            {
                Debug.Log($"{LogTag} SalesScreenView/controller not in scene — no active sales day.");
                return;
            }

            if (controller.IsDayCompleted)
            {
                Debug.Log($"{LogTag} Day already completed — ignored.");
                return;
            }

            Debug.Log($"{LogTag} Force-completing day {controller.Day} (zeroOut={zeroOut}).");
            controller.ForceCompleteDay(zeroOut);
        }
    }
}
