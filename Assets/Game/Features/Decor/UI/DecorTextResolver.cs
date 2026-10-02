using Game.Configs.Models;
using Game.Localization;

namespace Game.Decor.UI
{
    /// <summary>
    /// Resolves the player-facing name and description of a decor from its config, shared by
    /// <see cref="DecorInfoPopup"/> and the decor placement window so both read the same keys.
    /// </summary>
    public static class DecorTextResolver
    {
        // Decors authored without a descriptionKey (or blanked by an RC config override) fall back
        // to this generic line instead of rendering an empty label.
        private const string DescriptionFallbackKey = "ui.decor.description.fallback";

        public static string ResolveName(DecorConfig config)
            => string.IsNullOrEmpty(config?.DisplayNameKey)
                ? config?.Id
                : LocalizationLocator.GetOrKey(config.DisplayNameKey);

        public static string ResolveDescription(DecorConfig config)
            => LocalizationLocator.GetOrKey(
                string.IsNullOrWhiteSpace(config?.DescriptionKey)
                    ? DescriptionFallbackKey
                    : config.DescriptionKey);
    }
}
