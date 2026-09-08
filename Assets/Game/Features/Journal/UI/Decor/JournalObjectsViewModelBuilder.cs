using System;
using System.Collections.Generic;
using Game.Configs;
using Game.Configs.Models;
using Game.Decor;
using Game.Localization;

namespace Game.Journal.UI
{
    public sealed class JournalObjectsViewModelBuilder
    {
        public JournalObjectsViewModel Build(
            IReadOnlyList<string> activeDecorIds,
            IConfigsService configs,
            IDecorTotalEffectsProvider effects)
        {
            return new JournalObjectsViewModel(
                BuildObjects(activeDecorIds, configs),
                BuildBonuses(activeDecorIds, effects));
        }

        private static IReadOnlyList<JournalObjectItemModel> BuildObjects(
            IReadOnlyList<string> activeDecorIds,
            IConfigsService configs)
        {
            if (activeDecorIds == null || activeDecorIds.Count == 0 || configs == null)
                return Array.Empty<JournalObjectItemModel>();

            var result = new List<JournalObjectItemModel>(activeDecorIds.Count);
            for (var i = 0; i < activeDecorIds.Count; i++)
            {
                var id = activeDecorIds[i];
                if (string.IsNullOrEmpty(id)) continue;
                if (!configs.TryGet<DecorConfig>(id, out var config) || config == null) continue;
                result.Add(new JournalObjectItemModel(
                    id,
                    string.IsNullOrEmpty(config.DisplayNameKey)
                        ? id
                        : LocalizationLocator.GetOrKey(config.DisplayNameKey)));
            }

            return result;
        }

        private static IReadOnlyList<JournalBonusItemModel> BuildBonuses(
            IReadOnlyList<string> activeDecorIds,
            IDecorTotalEffectsProvider effects)
        {
            if (effects == null) return Array.Empty<JournalBonusItemModel>();

            var totals = effects.GetTotalEffects(activeDecorIds);
            if (totals == null || totals.Count == 0) return Array.Empty<JournalBonusItemModel>();

            var result = new List<JournalBonusItemModel>(totals.Count);
            for (var i = 0; i < totals.Count; i++)
            {
                var total = totals[i];
                var percentText = FormatPercent(total.Percent);
                switch (total.Kind)
                {
                    case DecorEffectKind.GenreSaleChance:
                        if (string.IsNullOrEmpty(total.Subject)) continue;
                        result.Add(new JournalBonusItemModel(
                            $"{percentText} {total.Subject} {LocalizationLocator.GetOrKey("ui.decor.bonus.sale_chance")}",
                            percentText,
                            total.Percent >= 0f,
                            total.Subject));
                        break;
                    case DecorEffectKind.CustomerTraffic:
                        result.Add(new JournalBonusItemModel(
                            $"{percentText} customers",
                            percentText,
                            total.Percent >= 0f,
                            null));
                        break;
                }
            }

            return result;
        }

        private static string FormatPercent(float percent)
            => percent >= 0f ? $"+{percent:0}%" : $"{percent:0}%";
    }
}
