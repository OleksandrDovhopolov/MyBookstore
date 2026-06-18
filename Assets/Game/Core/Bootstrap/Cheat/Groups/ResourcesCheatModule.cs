using System;
using System.Threading;
using cheatModule;
using Cysharp.Threading.Tasks;
using Game.Resources.API;
using UnityEngine;

namespace Game.Cheat
{
    public class ResourcesCheatModule : ICheatsModule
    {
        private const string CardsGroup = "Resources";
        private const string LogTag = "[ResourcesCheat]";
        private const string CheatReason = "cheat";

        private readonly IResourcesService _resources;
        private readonly CancellationToken _ct;

        public ResourcesCheatModule(IResourcesService resources, CancellationToken ct)
        {
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _ct = ct;
        }

        public void Initialize(ICheatsContainer cheatsContainer)
        {
            AddGoldButton(cheatsContainer, 100);
            AddGoldButton(cheatsContainer, 1000);
            AddGoldButton(cheatsContainer, 10000);
        }

        private void AddGoldButton(ICheatsContainer container, int amount)
        {
            container.AddItem<CheatButtonItem>(item =>
                item.OnClick($"+{amount} Gold", () => AddGoldAsync(amount).Forget())
                    .WithGroup(CardsGroup));
        }

        private async UniTaskVoid AddGoldAsync(int amount)
        {
            await _resources.AddAsync(ResourceIds.Gold, amount, CheatReason, _ct);
            Debug.Log($"{LogTag} +{amount} gold (total now {_resources.GetAmount(ResourceIds.Gold)}).");
        }
    }
}
