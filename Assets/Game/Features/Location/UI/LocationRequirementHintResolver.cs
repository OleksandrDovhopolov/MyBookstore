using System;
using Game.Configs;
using Game.Configs.Models;
using Game.Localization;

namespace Game.Location.UI
{
    public static class LocationRequirementHintResolver
    {
        private const string InventoryItemKind = "InventoryItem";
        private const string SoldTotalReasonKey = "soldTotal";
        private const string SoldGenrePrefix = "soldGenre.";
        private const string VisitLocationPrefix = "visitLocation.";

        public static LocationRequirementInfoWidgetData Resolve(
            IConfigsService configs,
            LocationRequirementRef requirement)
        {
            return requirement.Kind switch
            {
                LocationRequirementKind.Item => ResolveItem(configs, requirement.Key),
                LocationRequirementKind.Condition => ResolveCondition(configs, requirement.Key),
                _ => UnknownCondition()
            };
        }

        private static LocationRequirementInfoWidgetData ResolveItem(IConfigsService configs, string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return new LocationRequirementInfoWidgetData(
                    Localize("location.req.condition.title.unknown"),
                    Localize("location.req.source.unknown"));

            var hasQuestSource = HasQuestReward(configs, itemId);
            var hasShopSource = HasShopReward(configs, itemId);
            var sourceKey = hasQuestSource switch
            {
                true when hasShopSource => "location.req.source.both",
                true => "location.req.source.quest",
                _ => hasShopSource ? "location.req.source.shop" : "location.req.source.unknown"
            };

            return new LocationRequirementInfoWidgetData(
                ResolveItemTitle(configs, itemId),
                Localize(sourceKey));
        }

        private static LocationRequirementInfoWidgetData ResolveCondition(IConfigsService configs, string reasonKey)
        {
            if (string.Equals(reasonKey, SoldTotalReasonKey, StringComparison.Ordinal))
            {
                return new LocationRequirementInfoWidgetData(
                    Localize("location.req.condition.title.soldTotal"),
                    Localize("location.req.condition.soldTotal"));
            }

            if (!string.IsNullOrEmpty(reasonKey)
                && reasonKey.StartsWith(SoldGenrePrefix, StringComparison.Ordinal))
            {
                var genre = reasonKey.Substring(SoldGenrePrefix.Length);
                var title = string.IsNullOrEmpty(genre)
                    ? Localize("location.req.condition.title.soldGenre")
                    : Localize("location.req.condition.title.soldGenre", genre);
                return new LocationRequirementInfoWidgetData(
                    title,
                    Localize("location.req.condition.soldGenre"));
            }

            if (!string.IsNullOrEmpty(reasonKey)
                && reasonKey.StartsWith(VisitLocationPrefix, StringComparison.Ordinal))
            {
                var locationId = reasonKey.Substring(VisitLocationPrefix.Length);
                return new LocationRequirementInfoWidgetData(
                    ResolveLocationTitle(configs, locationId),
                    Localize("location.req.condition.visitLocation"));
            }

            return UnknownCondition();
        }

        private static LocationRequirementInfoWidgetData UnknownCondition()
            => new(
                Localize("location.req.condition.title.unknown"),
                Localize("location.req.condition.unknown"));

        private static bool HasQuestReward(IConfigsService configs, string itemId)
        {
            if (configs == null) return false;

            foreach (var quest in configs.GetAll<QuestConfig>())
            {
                var rewards = quest?.Rewards;
                if (rewards == null) continue;

                for (var i = 0; i < rewards.Length; i++)
                {
                    var reward = rewards[i];
                    if (reward == null) continue;
                    if (!string.Equals(reward.Kind, InventoryItemKind, StringComparison.Ordinal)) continue;
                    if (string.Equals(reward.Id, itemId, StringComparison.Ordinal))
                        return true;
                }
            }

            return false;
        }

        private static bool HasShopReward(IConfigsService configs, string itemId)
        {
            if (configs == null) return false;

            foreach (var lot in configs.GetAll<ShopConfig>())
            {
                var rewards = lot?.RewardItems;
                if (rewards == null) continue;

                for (var i = 0; i < rewards.Length; i++)
                {
                    var reward = rewards[i];
                    if (reward == null) continue;
                    if (!IsInventoryItemReward(reward)) continue;
                    if (string.Equals(reward.Id, itemId, StringComparison.Ordinal))
                        return true;
                }
            }

            return false;
        }

        private static bool IsInventoryItemReward(object reward)
        {
            var kind = reward?.GetType().GetProperty("Kind")?.GetValue(reward);
            return string.Equals(kind?.ToString(), InventoryItemKind, StringComparison.Ordinal);
        }

        private static string ResolveItemTitle(IConfigsService configs, string itemId)
        {
            if (configs == null)
                return itemId;

            if (configs.TryGet<QuestItemConfig>(itemId, out var questItem) && questItem != null)
                return ResolveLocalization(questItem.DisplayNameKey, itemId);

            if (configs.TryGet<ConsumableConfig>(itemId, out var consumable) && consumable != null)
                return ResolveLocalization(consumable.DisplayNameKey, itemId);

            if (configs.TryGet<DecorConfig>(itemId, out var decor) && decor != null)
                return ResolveLocalization(decor.DisplayNameKey, itemId);

            if (configs.TryGet<BookConfig>(itemId, out var book) && book != null)
                return ResolveLocalization(book.TitleKey, itemId);

            return itemId;
        }

        private static string ResolveLocationTitle(IConfigsService configs, string locationId)
        {
            if (string.IsNullOrEmpty(locationId))
                return Localize("location.req.condition.title.visitLocation");

            if (configs != null
                && configs.TryGet<LocationConfig>(locationId, out var location)
                && location != null)
            {
                return ResolveLocalization(location.DisplayNameKey, locationId);
            }

            return locationId;
        }

        private static string ResolveLocalization(string key, string fallback)
            => string.IsNullOrEmpty(key)
                ? fallback
                : LocalizationLocator.GetOrKey(key);

        private static string Localize(string key, params object[] args)
            => LocalizationLocator.GetOrKey(key, args);
    }
}
