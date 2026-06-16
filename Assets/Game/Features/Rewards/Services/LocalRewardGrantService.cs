using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Inventory.API;
using Game.Resources.API;
using Game.Rewards.API;
using UnityEngine;

namespace Game.Rewards.Services
{
    /// <summary>
    /// Phase 0 implementation of <see cref="IRewardGrantService"/>. Runs the input spec through any
    /// registered <see cref="IRewardSpecExpander"/>s, then writes each <see cref="RewardItem"/> straight
    /// to <see cref="IResourcesService"/> or <see cref="IInventoryService"/>.
    /// </summary>
    /// <remarks>
    /// Not transactional: a failure midway leaves player state partially mutated. This is accepted
    /// Phase 0 debt (see SHOP.md §12.4). Phase 1+ moves the dispatch server-side and the snapshot
    /// applier handles atomicity.
    /// </remarks>
    public sealed class LocalRewardGrantService : IRewardGrantService
    {
        private const string LogTag = "[Rewards]";

        private readonly IResourcesService _resources;
        private readonly IInventoryService _inventory;
        private readonly IReadOnlyList<IRewardSpecExpander> _expanders;

        public LocalRewardGrantService(
            IResourcesService resources,
            IInventoryService inventory,
            IReadOnlyList<IRewardSpecExpander> expanders)
        {
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _expanders = expanders ?? Array.Empty<IRewardSpecExpander>();
        }

        public async UniTask<RewardGrantResult> GrantAsync(RewardSpec spec, string source, CancellationToken ct)
        {
            if (spec == null)
                return RewardGrantResult.Fail("spec is null");

            var expanded = await ExpandAsync(spec, ct);

            for (var i = 0; i < expanded.Items.Count; i++)
            {
                var item = expanded.Items[i];
                if (item.Amount <= 0)
                    continue;

                switch (item.Kind)
                {
                    case RewardKind.Resource:
                        await _resources.AddAsync(item.Id, item.Amount, source, ct);
                        break;

                    case RewardKind.InventoryItem:
                        await _inventory.AddAsync(item.Id, item.Category, item.Amount, ct);
                        break;

                    default:
                        Debug.LogError($"{LogTag} Unknown RewardKind '{item.Kind}' in spec '{spec.Id}'.");
                        return RewardGrantResult.Fail($"unknown_kind:{item.Kind}");
                }
            }

            return RewardGrantResult.Ok(expanded);
        }

        private async UniTask<RewardSpec> ExpandAsync(RewardSpec spec, CancellationToken ct)
        {
            for (var i = 0; i < _expanders.Count; i++)
            {
                var expander = _expanders[i];
                if (expander.CanExpand(spec))
                    return await expander.ExpandAsync(spec, ct);
            }

            return spec;
        }
    }
}
