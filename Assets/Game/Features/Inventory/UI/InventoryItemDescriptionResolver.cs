using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;

namespace Game.Inventory.UI
{
    /// <summary>
    /// Resolves the localization key of the "what does this item do" hint shown in the inventory
    /// item widget. Row styles are not unique — consumables and book genre rows share
    /// <see cref="InventoryRowStyle.Default"/> — so the id itself decides which config to read.
    /// </summary>
    public static class InventoryItemDescriptionResolver
    {
        /// <summary>
        /// Returns the description key for a row, or null when nothing is authored for it
        /// (the caller then falls back to its own generic key).
        /// </summary>
        public static string ResolveDescriptionKey(
            IConfigsService configs,
            string itemId,
            InventoryRowStyle style)
        {
            if (configs == null || string.IsNullOrEmpty(itemId)) return null;

            // Quest items are the only style with a dedicated config, so probe it first for them.
            if (style == InventoryRowStyle.QuestItem)
                return QuestItemKey(configs, itemId) ?? ConsumableKey(configs, itemId);

            // TryGet is silent on a miss (unlike Get), so probing every candidate costs no log spam.
            var key = ConsumableKey(configs, itemId) ?? QuestItemKey(configs, itemId);
            if (key != null) return key;

            // Book rows are genre aggregates: the id is a BookGenre config value, not an item id.
            return BookGenreExtensions.TryParseGenre(itemId, out var genre)
                ? genre.ToDescriptionLocalizationKey()
                : null;
        }

        private static string ConsumableKey(IConfigsService configs, string itemId)
            => configs.TryGet<ConsumableConfig>(itemId, out var config)
                ? Normalize(config?.DescriptionKey)
                : null;

        private static string QuestItemKey(IConfigsService configs, string itemId)
            => configs.TryGet<QuestItemConfig>(itemId, out var config)
                ? Normalize(config?.DescriptionKey)
                : null;

        // A config that exists but carries no key is a miss, not an empty description.
        private static string Normalize(string key)
            => string.IsNullOrWhiteSpace(key) ? null : key;
    }
}
