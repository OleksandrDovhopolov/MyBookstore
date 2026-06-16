using System.Collections.Generic;
using System.Text;
using System.Threading;
using Book.Sell.API;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Decor.Services;
using Game.Inventory.API;
using Game.UI;
using Game.UI.Common;
using UnityEngine;
using VContainer;

namespace Game.Decor.UI
{
    /// <summary>
    /// UISystem window replacing the MonoBehaviour <c>DecorPlacementScreenView</c>. Layout follows
    /// docs/INPROGRESS/Decor.md §7.1: summary panel + slot list + inventory list. Negative-effect
    /// placements still gated by ConfirmDialog. Phase 0 stub geometry — real visual is Phase 2+.
    /// </summary>
    [Window("DecorPlacementWindow", WindowType.Page)]
    public sealed class DecorPlacementWindow : WindowController<DecorPlacementWindowView>
    {
        private IDecorPlacementService _placement;
        private IConfigsService _configs;
        private IInventoryService _inventory;
        private IDecorModifierProvider _decorModifier;

        private CancellationTokenSource _cts;
        private readonly List<DecorSlotRowView> _slotRowPool = new();
        private readonly List<DecorInventoryRowView> _inventoryRowPool = new();

        [Inject]
        public void InjectServices(
            IDecorPlacementService placement,
            IConfigsService configs,
            IInventoryService inventory,
            IDecorModifierProvider decorModifier)
        {
            _placement = placement;
            _configs = configs;
            _inventory = inventory;
            _decorModifier = decorModifier;
        }

        protected override void OnInit()
        {
            _cts = new CancellationTokenSource();

            if (View.SlotRowTemplate != null) View.SlotRowTemplate.gameObject.SetActive(false);
            if (View.InventoryRowTemplate != null) View.InventoryRowTemplate.gameObject.SetActive(false);

            if (View.ClearAllButton != null) View.ClearAllButton.onClick.AddListener(OnClearAllClicked);
            if (View.CloseButton != null) View.CloseButton.onClick.AddListener(OnCloseClicked);
        }

        protected override void OnShowStart()
        {
            if (_placement != null) _placement.PlacementChanged += Render;
            if (_inventory != null) _inventory.Changed += OnInventoryChanged;
            Render();
        }

        protected override void OnHideStart(bool isClosed)
        {
            if (_placement != null) _placement.PlacementChanged -= Render;
            if (_inventory != null) _inventory.Changed -= OnInventoryChanged;
        }

        protected override void OnDispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (View == null) return;
            if (View.ClearAllButton != null) View.ClearAllButton.onClick.RemoveListener(OnClearAllClicked);
            if (View.CloseButton != null) View.CloseButton.onClick.RemoveListener(OnCloseClicked);
        }

        private void OnInventoryChanged(InventoryChangeEvent _) => Render();

        private void Render()
        {
            if (_placement == null) return;
            RenderSummary();
            RenderSlots();
            RenderInventory();
        }

        private void RenderSummary()
        {
            if (View.SummaryLabel == null) return;

            var activeIds = _placement.GetActiveDecorIds();
            var allGenres = CollectAllGenres();
            var sb = new StringBuilder();
            sb.AppendLine("<b>Active Decor Effects</b>");

            var anyEffect = false;
            var anyCap = false;
            foreach (var genre in allGenres)
            {
                var multiplier = _decorModifier.GetGenreMultiplier(genre, activeIds);
                if (Mathf.Approximately(multiplier, 1f)) continue;
                anyEffect = true;
                var color = multiplier < 1f ? View.NegativeColor : View.PositiveColor;
                var hex = ColorUtility.ToHtmlStringRGB(color);
                sb.AppendLine($"  {genre,-12} <color=#{hex}>×{multiplier:0.00}</color>");

                if (multiplier >= ConfigBasedDecorModifierProvider.SoftCapMax - 0.0001f)
                    anyCap = true;
            }

            if (!anyEffect) sb.AppendLine("  (no active decor effects)");
            View.SummaryLabel.text = sb.ToString();

            if (View.CapHintLabel != null)
            {
                if (anyCap)
                {
                    View.CapHintLabel.text = $"Soft cap reached on at least one genre (×{ConfigBasedDecorModifierProvider.SoftCapMax:0.0}).";
                    View.CapHintLabel.color = View.CapHintColor;
                    View.CapHintLabel.gameObject.SetActive(true);
                }
                else
                {
                    View.CapHintLabel.gameObject.SetActive(false);
                }
            }
        }

        private void RenderSlots()
        {
            ClearRows(_slotRowPool);
            var shop = _configs.Get<BookShopConfig>(DecorPlacementService.HardcodedBookShopId);
            if (shop?.DecorSlots == null) return;

            foreach (var slot in shop.DecorSlots)
            {
                if (slot == null) continue;
                var row = SpawnSlotRow();
                if (row == null) continue;
                var decorId = _placement.GetDecorInSlot(slot.Id);
                var decorConfig = string.IsNullOrEmpty(decorId) ? null : _configs.Get<DecorConfig>(decorId);
                row.Bind(slot, decorConfig, View.PositiveColor, View.NegativeColor);
                row.OnPlaceRequested = () => OnPlaceClicked(slot);
                row.OnUnplaceRequested = () => OnUnplaceClicked(slot);
                row.gameObject.SetActive(true);
            }
        }

        private void RenderInventory()
        {
            ClearRows(_inventoryRowPool);
            var items = _inventory.GetByCategory(InventoryCategories.Decor);
            foreach (var item in items)
            {
                var config = _configs.Get<DecorConfig>(item.ItemId);
                if (config == null) continue;
                var placedSlotId = FindPlacedSlot(item.ItemId);
                var row = SpawnInventoryRow();
                if (row == null) continue;
                row.Bind(config, placedSlotId, View.PositiveColor, View.NegativeColor);
                row.gameObject.SetActive(true);
            }
        }

        private string FindPlacedSlot(string decorId)
        {
            foreach (var entry in _placement.GetAllPlacements())
            {
                if (string.Equals(entry.DecorId, decorId, System.StringComparison.OrdinalIgnoreCase))
                    return entry.SlotId;
            }
            return null;
        }

        private DecorSlotRowView SpawnSlotRow()
        {
            if (View.SlotRowTemplate == null || View.SlotListRoot == null) return null;
            var row = Object.Instantiate(View.SlotRowTemplate, View.SlotListRoot);
            _slotRowPool.Add(row);
            return row;
        }

        private DecorInventoryRowView SpawnInventoryRow()
        {
            if (View.InventoryRowTemplate == null || View.InventoryListRoot == null) return null;
            var row = Object.Instantiate(View.InventoryRowTemplate, View.InventoryListRoot);
            _inventoryRowPool.Add(row);
            return row;
        }

        private static void ClearRows<T>(List<T> pool) where T : MonoBehaviour
        {
            for (var i = 0; i < pool.Count; i++)
                if (pool[i] != null) Object.Destroy(pool[i].gameObject);
            pool.Clear();
        }

        private HashSet<string> CollectAllGenres()
        {
            var set = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var books = _configs.GetAll<BookConfig>();
            foreach (var b in books)
                if (!string.IsNullOrEmpty(b.Genre)) set.Add(b.Genre);
            return set;
        }

        private void OnPlaceClicked(DecorSlot slot) => PickAndPlaceAsync(slot).Forget();

        private async UniTaskVoid PickAndPlaceAsync(DecorSlot slot)
        {
            var candidate = PickFirstCompatibleUnplaced(slot);
            if (candidate == null)
            {
                Debug.Log("[DecorPlacementWindow] No compatible unplaced decor available for this slot.");
                return;
            }

            if (HasNegativeEffect(candidate))
            {
                var args = new ConfirmDialogArgs(
                    title: $"Place {candidate.DisplayName}?",
                    body: BuildNegativeWarning(candidate),
                    confirmLabel: "Place anyway",
                    cancelLabel: "Cancel");

                var dialog = await UIManager.ShowAsync<ConfirmDialog>(args, _cts.Token);
                if (dialog == null) return;
                var result = await dialog.WaitForResultAsync<ConfirmDialogResult>(_cts.Token);
                if (result != ConfirmDialogResult.Confirmed) return;
            }

            await _placement.PlaceAsync(candidate.Id, slot.Id, _cts.Token);
        }

        private DecorConfig PickFirstCompatibleUnplaced(DecorSlot slot)
        {
            var items = _inventory.GetByCategory(InventoryCategories.Decor);
            foreach (var item in items)
            {
                if (!string.IsNullOrEmpty(FindPlacedSlot(item.ItemId))) continue;
                var config = _configs.Get<DecorConfig>(item.ItemId);
                if (config == null) continue;
                if (config.PositionType != slot.PositionType) continue;
                if ((int)config.Size > (int)slot.MaxSize) continue;
                return config;
            }
            return null;
        }

        private static bool HasNegativeEffect(DecorConfig config)
        {
            if (config?.GenreMultipliers == null) return false;
            foreach (var mod in config.GenreMultipliers)
                if (mod != null && mod.Multiplier < 1f) return true;
            return false;
        }

        private static string BuildNegativeWarning(DecorConfig config)
        {
            var sb = new StringBuilder("This decor will REDUCE: ");
            var first = true;
            foreach (var mod in config.GenreMultipliers)
            {
                if (mod == null || mod.Multiplier >= 1f) continue;
                if (!first) sb.Append(", ");
                first = false;
                var percent = Mathf.RoundToInt((1f - mod.Multiplier) * 100f);
                sb.Append($"{mod.Genre} −{percent}%");
            }
            sb.Append(". Continue?");
            return sb.ToString();
        }

        private void OnUnplaceClicked(DecorSlot slot) => _placement.UnplaceAsync(slot.Id, _cts.Token).Forget();

        private void OnClearAllClicked() => ClearAllConfirmedAsync().Forget();

        private async UniTaskVoid ClearAllConfirmedAsync()
        {
            var args = new ConfirmDialogArgs(
                title: "Clear all decor placements?",
                body: "Every slot will be emptied. Items stay in inventory.",
                confirmLabel: "Clear",
                cancelLabel: "Cancel");
            var dialog = await UIManager.ShowAsync<ConfirmDialog>(args, _cts.Token);
            if (dialog == null) return;
            var result = await dialog.WaitForResultAsync<ConfirmDialogResult>(_cts.Token);
            if (result != ConfirmDialogResult.Confirmed) return;
            await _placement.ClearAllAsync(_cts.Token);
        }

        private void OnCloseClicked()
        {
            //UIManager.HideAsync<DecorPlacementWindow>().Forget();
            CloseAsync().Forget();
        }
    }
}
