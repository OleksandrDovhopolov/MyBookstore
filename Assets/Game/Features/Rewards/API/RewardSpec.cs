using System;
using System.Collections.Generic;

namespace Game.Rewards.API
{
    /// <summary>
    /// A named bundle of <see cref="RewardItem"/>s that can be granted as a single unit. Phase 0:
    /// specs are produced by callers (e.g. <c>ShopService</c> reads from a static registry). Phase 1+:
    /// specs come from server responses or a JSON catalog.
    /// </summary>
    /// <remarks>
    /// A spec may be "abstract" (e.g. <c>book_box_common_15</c> with a single placeholder item) and get
    /// expanded into concrete items by an <see cref="IRewardSpecExpander"/> before dispatch. Callers
    /// should treat the spec returned in <c>RewardGrantResult.Granted</c> as the source of truth for UI.
    /// </remarks>
    public sealed class RewardSpec
    {
        public string Id { get; }
        public IReadOnlyList<RewardItem> Items { get; }

        public RewardSpec(string id, IReadOnlyList<RewardItem> items)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Items = items ?? Array.Empty<RewardItem>();
        }
    }
}
