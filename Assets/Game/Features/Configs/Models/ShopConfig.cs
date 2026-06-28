using Game.Rewards.API;
using Game.Shop.API;

namespace Game.Configs.Models
{
    /// <summary>
    /// Один лот магазина. Файл: <c>shop.json</c> (JSON-массив).
    /// </summary>
    /// <remarks>
    /// Reward задаётся inline: <see cref="RewardId"/> определяет идентичность спеки (на него матчатся
    /// expander'ы вроде book-box в PR4), а <see cref="RewardItems"/> — конкретное содержимое. Пустой
    /// <c>RewardItems</c> допустим (например, для book-box лотов, где expander сам собирает items).
    /// </remarks>
    [ConfigFile("shop")]
    public sealed class ShopConfig : IConfig
    {
        public string Id { get; set; }
        public string StorefrontId { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public ShopPriceData Price { get; set; }
        public string RewardId { get; set; }
        public RewardItemData[] RewardItems { get; set; }
        public ShopLotLimitData Limit { get; set; }
    }

    public sealed class ShopPriceData
    {
        public string Currency { get; set; }
        public int Amount { get; set; }
    }

    public sealed class ShopLotLimitData
    {
        public ShopLimitMode Mode { get; set; }
        public int? MaxPurchases { get; set; }
    }

    public sealed class RewardItemData
    {
        public string Id { get; set; }
        public string Category { get; set; }
        public int Amount { get; set; }
        public RewardKind Kind { get; set; }
    }
}
