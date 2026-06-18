using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Decor
{
    /// <summary>
    /// First-day newspaper offers — one free decor + one paid decor for gold.
    /// Phase 0: hardcoded ids (vintage_globe + coffee_pot). Phase 3+: data-driven via NewspaperConfig.
    /// </summary>
    public interface IDecorRewardService
    {
        bool HasFreeDecorAvailable { get; }
        bool HasPaidOfferAvailable { get; }

        string OfferedFreeDecorId { get; }
        string OfferedPaidDecorId { get; }
        int OfferedPaidPrice { get; }

        //TODO ClaimFreeDecorAsync this method shouldnt exist. Buy with price 0 
        UniTask<bool> ClaimFreeDecorAsync(CancellationToken ct);
        UniTask<bool> BuyOfferedDecorAsync(CancellationToken ct);
    }
}
