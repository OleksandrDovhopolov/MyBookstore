using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Services;
using Game.UI;

namespace Book.Sell.UI
{
    /// <summary>
    /// Carries the gameplay-scoped <see cref="ISalesDayController"/> + the <see cref="DialoguePayload"/> into
    /// <see cref="DialogWindow"/>. Mirrors <see cref="RecommendationMinigameArgs"/>: the window is created by
    /// the bootstrap-scoped window factory, which cannot inject the gameplay-scope controller, so the
    /// presenter (which already has it) passes it through here.
    ///
    /// <see cref="Controller"/> may be null in debug/cheat mode (opened outside a sales scene, §A6): the
    /// window then just renders the graph and closes, with no sim/lock to resolve. <see cref="Customer"/> is
    /// carried for a future world-HUD anchor (2.2) and is unused by the 2.1 window.
    /// </summary>
    public sealed class DialogWindowArgs : WindowArgs
    {
        public ISalesDayController Controller { get; }
        public DialoguePayload Payload { get; }
        public Book.Sell.Domain.Customer Customer { get; }

        public DialogWindowArgs(ISalesDayController controller, DialoguePayload payload, Book.Sell.Domain.Customer customer = null)
        {
            Controller = controller;
            Payload = payload;
            Customer = customer;
        }
    }
}
