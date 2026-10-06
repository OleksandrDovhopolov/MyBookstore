using System;
using System.Collections.Generic;
using Game.Conditions.API;
using Game.Shop.API;
using Newtonsoft.Json.Linq;

namespace Game.Shop.Conditions
{
    /// <summary>
    /// Builds <see cref="ShopPurchasesCondition"/> from
    /// <c>{ "type": "shopPurchases", "storefrontIds": ["newspaper.books"], "min": 3 }</c>.
    ///
    /// Holds a lazy <see cref="Func{TResult}"/> for <see cref="IShopService"/> so building the
    /// <see cref="IConditionFactory"/> collection never forces the shop service — ShopService resolves
    /// <c>Func&lt;IConditionParser&gt;</c> itself, and a direct injection here would close that loop
    /// (see the comment in ShopVContainerBindings). The service is resolved on the first
    /// <see cref="Changed"/> subscription, by which point the analytics entry point has already built it.
    /// </summary>
    public sealed class ShopPurchasesConditionFactory : IConditionFactory, IConditionChangeSource
    {
        public const string TypeId = "shopPurchases";

        private readonly Func<IShopService> _shop;

        private Action _changed;
        private bool _subscribed;

        public ShopPurchasesConditionFactory(Func<IShopService> shop)
            => _shop = shop ?? throw new ArgumentNullException(nameof(shop));

        public string Type => TypeId;

        /// <summary>Raised after every successful purchase, so quest/unlock consumers re-evaluate.</summary>
        public event Action Changed
        {
            add
            {
                _changed += value;
                EnsureSubscribed();
            }
            remove => _changed -= value;
        }

        public ICondition Create(JObject node)
        {
            var storefronts = ReadStorefronts(node);
            if (storefronts.Length == 0)
                throw new ArgumentException("missing 'storefrontIds'");

            var min = node.Value<int?>("min") ?? 1;
            return new ShopPurchasesCondition(_shop, storefronts, min);
        }

        private void EnsureSubscribed()
        {
            if (_subscribed) return;

            var shop = _shop();
            if (shop == null) return;

            shop.LotPurchased += OnLotPurchased;
            _subscribed = true;
        }

        private void OnLotPurchased(ShopPurchaseEvent _) => _changed?.Invoke();

        private static string[] ReadStorefronts(JObject node)
        {
            var result = new List<string>();

            if (node?["storefrontIds"] is JArray array)
            {
                foreach (var token in array)
                {
                    var id = token?.Value<string>();
                    if (!string.IsNullOrWhiteSpace(id)) result.Add(id.Trim());
                }
            }

            // Single-storefront shorthand, same spelling as the lot field in shop.json.
            var single = node?.Value<string>("storefrontId");
            if (!string.IsNullOrWhiteSpace(single)) result.Add(single.Trim());

            return result.ToArray();
        }
    }
}
