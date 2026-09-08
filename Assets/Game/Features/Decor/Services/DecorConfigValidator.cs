using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using Game.Shop.API;
using UnityEngine;
using VContainer.Unity;

namespace Game.Decor.Services
{
    /// <summary>
    /// Validates DecorConfig, BookShopConfig.DecorSlots, and decor rewards at boot.
    /// Awaits <see cref="IConfigsService.WarmupAsync"/>
    /// first so configs are guaranteed to be loaded regardless of entry-point registration order.
    /// In Editor, errors throw to block Play mode; in runtime builds, errors are logged and the
    /// affected entries get effectively ignored downstream.
    /// </summary>
    public sealed class DecorConfigValidator : IAsyncStartable
    {
        private const string LogTag = "[DecorValidator]";

        private readonly IConfigsService _configs;

        public DecorConfigValidator(IConfigsService configs)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            await _configs.WarmupAsync(cancellation);

            var report = Validate();

            for (var i = 0; i < report.Warnings.Count; i++)
                Debug.LogWarning($"{LogTag} {report.Warnings[i]}");

            if (!report.HasErrors) return;

            for (var i = 0; i < report.Errors.Count; i++)
                Debug.LogError($"{LogTag} {report.Errors[i]}");

#if UNITY_EDITOR
            throw new InvalidOperationException(
                $"{LogTag} {report.Errors.Count} decor config error(s). See console.\n{report.FormatErrors()}");
#endif
        }

        public ValidationReport Validate()
        {
            var report = new ValidationReport();
            var decorIds = ValidateDecors(report);
            ValidateBookShops(report);
            ValidateDecorSlotCoverage(report);
            var decorReferences = CollectDecorReferences();
            var decorIdSet = new HashSet<string>(decorIds, StringComparer.OrdinalIgnoreCase);
            ValidateDecorReferences(report, decorIdSet, decorReferences);
            ValidateDecorStorefronts(report, decorReferences);
            ValidateDecorReachability(report, decorIds, decorReferences);
            return report;
        }

        private List<string> ValidateDecors(ValidationReport report)
        {
            var decors = _configs.GetAll<DecorConfig>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var orderedIds = new List<string>(decors.Count);

            var knownGenres = CollectKnownGenres();

            for (var i = 0; i < decors.Count; i++)
            {
                var decor = decors[i];

                if (string.IsNullOrEmpty(decor.Id))
                {
                    report.Errors.Add($"DecorConfig at index {i} has empty Id.");
                    continue;
                }

                if (!ids.Add(decor.Id))
                {
                    report.Errors.Add($"Duplicate DecorConfig Id '{decor.Id}'.");
                }
                else
                {
                    orderedIds.Add(decor.Id);
                }

                if (string.IsNullOrEmpty(decor.DisplayNameKey))
                    report.Warnings.Add($"Decor '{decor.Id}' has empty DisplayNameKey.");

                if (!Enum.IsDefined(typeof(DecorPositionType), decor.PositionType))
                    report.Errors.Add($"Decor '{decor.Id}' has invalid PositionType ({(int)decor.PositionType}).");

                if (!Enum.IsDefined(typeof(DecorSize), decor.Size))
                    report.Errors.Add($"Decor '{decor.Id}' has invalid Size ({(int)decor.Size}).");

                if (decor.BasePrice < 0)
                    report.Warnings.Add($"Decor '{decor.Id}' has negative BasePrice ({decor.BasePrice}).");

                if (decor.GenreMultipliers != null)
                {
                    for (var j = 0; j < decor.GenreMultipliers.Length; j++)
                    {
                        var mod = decor.GenreMultipliers[j];
                        if (mod == null)
                        {
                            report.Errors.Add($"Decor '{decor.Id}' has null entry at GenreMultipliers[{j}].");
                            continue;
                        }
                        if (mod.Multiplier <= 0f)
                        {
                            report.Errors.Add($"Decor '{decor.Id}' GenreMultipliers[{j}] has non-positive Multiplier ({mod.Multiplier}) — entry ignored.");
                            continue;
                        }
                        if (string.IsNullOrEmpty(mod.Genre))
                        {
                            report.Errors.Add($"Decor '{decor.Id}' GenreMultipliers[{j}] has empty Genre.");
                            continue;
                        }
                        if (knownGenres.Count > 0 && !knownGenres.Contains(mod.Genre))
                        {
                            report.Warnings.Add($"Decor '{decor.Id}' references unknown genre '{mod.Genre}' (not present in any BookConfig).");
                        }
                    }
                }
            }

            return orderedIds;
        }

        private void ValidateBookShops(ValidationReport report)
        {
            var shops = _configs.GetAll<BookShopConfig>();
            for (var i = 0; i < shops.Count; i++)
            {
                var shop = shops[i];
                if (shop?.DecorSlots == null) continue;

                var slotIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (var j = 0; j < shop.DecorSlots.Length; j++)
                {
                    var slot = shop.DecorSlots[j];
                    if (slot == null)
                    {
                        report.Errors.Add($"BookShop '{shop.Id}' DecorSlots[{j}] is null.");
                        continue;
                    }

                    if (string.IsNullOrEmpty(slot.Id))
                    {
                        report.Errors.Add($"BookShop '{shop.Id}' DecorSlots[{j}] has empty Id.");
                        continue;
                    }

                    if (!slotIds.Add(slot.Id))
                        report.Errors.Add($"BookShop '{shop.Id}' has duplicate slot Id '{slot.Id}'.");

                    if (!Enum.IsDefined(typeof(DecorPositionType), slot.PositionType))
                        report.Errors.Add($"BookShop '{shop.Id}' slot '{slot.Id}' has invalid PositionType.");

                    if (!Enum.IsDefined(typeof(DecorSize), slot.MaxSize))
                        report.Errors.Add($"BookShop '{shop.Id}' slot '{slot.Id}' has invalid MaxSize.");
                }
            }
        }

        private void ValidateDecorSlotCoverage(ValidationReport report)
        {
            var decors = _configs.GetAll<DecorConfig>();
            var shops = _configs.GetAll<BookShopConfig>();
            var slots = new List<(string ShopId, DecorSlot Slot)>();

            for (var i = 0; i < shops.Count; i++)
            {
                var shop = shops[i];
                if (shop?.DecorSlots == null) continue;

                for (var j = 0; j < shop.DecorSlots.Length; j++)
                {
                    var slot = shop.DecorSlots[j];
                    if (!IsValidSlotForCoverage(slot)) continue;
                    slots.Add((shop.Id, slot));

                    var hasFittingDecor = false;
                    for (var k = 0; k < decors.Count; k++)
                    {
                        var decor = decors[k];
                        if (IsValidDecorForCoverage(decor) && IsDecorCompatibleWithSlot(decor, slot))
                        {
                            hasFittingDecor = true;
                            break;
                        }
                    }

                    if (!hasFittingDecor)
                        report.Errors.Add(
                            $"BookShop '{shop.Id}' slot '{slot.Id}' has no fitting DecorConfig for {slot.PositionType} <= {slot.MaxSize}.");
                }
            }

            for (var i = 0; i < decors.Count; i++)
            {
                var decor = decors[i];
                if (!IsValidDecorForCoverage(decor)) continue;

                var fitsAnySlot = false;
                for (var j = 0; j < slots.Count; j++)
                {
                    if (IsDecorCompatibleWithSlot(decor, slots[j].Slot))
                    {
                        fitsAnySlot = true;
                        break;
                    }
                }

                if (!fitsAnySlot)
                    report.Warnings.Add(
                        $"Decor '{decor.Id}' fits no BookShop slot for {decor.PositionType} / {decor.Size} — it cannot be placed.");
            }
        }

        private HashSet<string> CollectKnownGenres()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var books = _configs.GetAll<BookConfig>();
            for (var i = 0; i < books.Count; i++)
            {
                if (!string.IsNullOrEmpty(books[i].PrimaryGenre))
                    set.Add(books[i].PrimaryGenre);
            }
            return set;
        }

        private List<DecorReference> CollectDecorReferences()
        {
            var references = new List<DecorReference>();
            CollectShopDecorReferences(references);
            CollectQuestDecorReferences(references);
            return references;
        }

        private void CollectShopDecorReferences(List<DecorReference> references)
        {
            var lots = _configs.GetAll<ShopConfig>();
            for (var i = 0; i < lots.Count; i++)
            {
                var lot = lots[i];
                if (lot?.RewardItems == null) continue;

                for (var j = 0; j < lot.RewardItems.Length; j++)
                {
                    var item = lot.RewardItems[j];
                    if (!IsDecorReward(item?.Id, item?.Category)) continue;

                    references.Add(new DecorReference(
                        item.Id,
                        $"Shop lot '{lot.Id}'",
                        lot.Id,
                        lot.StorefrontId,
                        isShopLot: true));
                }
            }
        }

        private void CollectQuestDecorReferences(List<DecorReference> references)
        {
            var quests = _configs.GetAll<QuestConfig>();
            for (var i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];
                if (quest?.Rewards == null) continue;

                for (var j = 0; j < quest.Rewards.Length; j++)
                {
                    var reward = quest.Rewards[j];
                    if (!IsDecorReward(reward?.Id, reward?.Category)) continue;

                    references.Add(new DecorReference(
                        reward.Id,
                        $"Quest '{quest.Id}'",
                        null,
                        null,
                        isShopLot: false));
                }
            }
        }

        private static bool IsDecorReward(string id, string category) =>
            !string.IsNullOrEmpty(id)
            && string.Equals(category, InventoryCategories.Decor, StringComparison.OrdinalIgnoreCase);

        private static bool IsValidDecorForCoverage(DecorConfig decor)
            => decor != null
               && !string.IsNullOrEmpty(decor.Id)
               && Enum.IsDefined(typeof(DecorPositionType), decor.PositionType)
               && Enum.IsDefined(typeof(DecorSize), decor.Size);

        private static bool IsValidSlotForCoverage(DecorSlot slot)
            => slot != null
               && !string.IsNullOrEmpty(slot.Id)
               && Enum.IsDefined(typeof(DecorPositionType), slot.PositionType)
               && Enum.IsDefined(typeof(DecorSize), slot.MaxSize);

        private static bool IsDecorCompatibleWithSlot(DecorConfig decor, DecorSlot slot)
            => decor.PositionType == slot.PositionType
               && (int)decor.Size <= (int)slot.MaxSize;

        private static void ValidateDecorReferences(
            ValidationReport report,
            HashSet<string> decorIdSet,
            List<DecorReference> references)
        {
            var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < references.Count; i++)
            {
                var reference = references[i];
                if (decorIdSet.Contains(reference.DecorId)) continue;

                var key = $"{reference.Origin}|{reference.DecorId}";
                if (reported.Add(key))
                    report.Errors.Add($"{reference.Origin} grants decor '{reference.DecorId}' which has no DecorConfig — reward will be unusable.");
            }
        }

        private static void ValidateDecorStorefronts(ValidationReport report, List<DecorReference> references)
        {
            var reportedLots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < references.Count; i++)
            {
                var reference = references[i];
                if (!reference.IsShopLot) continue;
                if (string.Equals(reference.StorefrontId, NewspaperShopLotIds.StorefrontDecor, StringComparison.OrdinalIgnoreCase)) continue;
                if (!reportedLots.Add(reference.ShopLotId)) continue;

                report.Warnings.Add(
                    $"Shop lot '{reference.ShopLotId}' grants decor but sits in storefront '{reference.StorefrontId}' (expected '{NewspaperShopLotIds.StorefrontDecor}') — it will not appear in the shop.");
            }
        }

        private static void ValidateDecorReachability(
            ValidationReport report,
            List<string> decorIds,
            List<DecorReference> references)
        {
            var grantedDecorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < references.Count; i++)
                grantedDecorIds.Add(references[i].DecorId);

            for (var i = 0; i < decorIds.Count; i++)
            {
                var decorId = decorIds[i];
                if (!grantedDecorIds.Contains(decorId))
                    report.Warnings.Add($"Decor '{decorId}' is not granted by any shop lot or quest reward — unreachable by the player.");
            }
        }

        private readonly struct DecorReference
        {
            public readonly string DecorId;
            public readonly string Origin;
            public readonly string ShopLotId;
            public readonly string StorefrontId;
            public readonly bool IsShopLot;

            public DecorReference(string decorId, string origin, string shopLotId, string storefrontId, bool isShopLot)
            {
                DecorId = decorId;
                Origin = origin;
                ShopLotId = shopLotId;
                StorefrontId = storefrontId;
                IsShopLot = isShopLot;
            }
        }
    }
}
