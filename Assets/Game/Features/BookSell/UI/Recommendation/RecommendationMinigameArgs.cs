using Book.Sell.Services;
using Game.UI;

namespace Book.Sell.UI
{
    /// <summary>
    /// Carries the gameplay-scoped <see cref="ISalesDayController"/> into <see cref="RecommendationMinigameWindow"/>.
    /// The window is created by the bootstrap-scoped <c>AddressablesWindowFactory</c>, which cannot inject
    /// the controller (it lives in the Gameplay child scope), so <see cref="SalesScreenView"/> — which already
    /// has it injected — passes it through these args.
    /// </summary>
    public sealed class RecommendationMinigameArgs : WindowArgs
    {
        public ISalesDayController Controller { get; }

        public RecommendationMinigameArgs(ISalesDayController controller)
        {
            Controller = controller;
        }
    }
}
