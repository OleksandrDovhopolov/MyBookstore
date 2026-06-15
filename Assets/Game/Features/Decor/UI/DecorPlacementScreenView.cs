using System.Collections.Generic;
using System.Text;
using System.Threading;
using Book.Sell.API;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using Game.UI;
using Game.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.Decor.UI
{
    /// <summary>
    /// Debug uGUI view for placing decor items into shop slots. Stub for Phase 0 — text and buttons
    /// only, no world-space slot anchors. Layout follows docs/INPROGRESS/Decor.md §7.1:
    /// 1) Active Decor Effects summary panel at the top (with soft cap indicator)
    /// 2) Slot list with per-slot effects card
    /// 3) Inventory list at the bottom
    /// Place actions on decors that carry any negative multiplier show a ConfirmDialog first.
    /// </summary>
    public sealed class DecorPlacementScreenView : MonoBehaviour
    {
        [Header("Summary panel")]
        [SerializeField] private TextMeshProUGUI _summaryLabel;
        [SerializeField] private TextMeshProUGUI _capHintLabel;

        [Header("Lists")]
        [SerializeField] private Transform _slotListRoot;
        [SerializeField] private Transform _inventoryListRoot;

        [Header("Templates (single child each)")]
        [SerializeField] private DecorSlotRowView _slotRowTemplate;
        [SerializeField] private DecorInventoryRowView _inventoryRowTemplate;

        [Header("Footer buttons")]
        [SerializeField] private Button _clearAllButton;
        [SerializeField] private Button _closeButton;

        [Header("Colors")]
        [SerializeField] private Color _positiveColor = new(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color _negativeColor = new(0.9f, 0.25f, 0.25f);
        [SerializeField] private Color _capHintColor = new(0.95f, 0.85f, 0.2f);

        private IDecorPlacementService _placement;
        private IConfigsService _configs;
        private IInventoryService _inventory;
        private IDecorModifierProvider _decorModifier;
        private IUIManager _uiManager;

        private readonly CancellationTokenSource _cts = new();
        private readonly List<DecorSlotRowView> _slotRowPool = new();
        private readonly List<DecorInventoryRowView> _inventoryRowPool = new();

        [Inject]
        public void Construct(
            IDecorPlacementService placement,
            IConfigsService configs,
            IInventoryService inventory,
            IDecorModifierProvider decorModifier,
            IUIManager uiManager)
        {
            _placement = placement;
            _configs = configs;
            _inventory = inventory;
            _decorModifier = decorModifier;
            _uiManager = uiManager;
        }

        private void Awake()
        {
            if (_slotRowTemplate != null) _slotRowTemplate.gameObject.SetActive(false);
            if (_inventoryRowTemplate != null) _inventoryRowTemplate.gameObject.SetActive(false);

            if (_clearAllButton != null) _clearAllButton.onClick.AddListener(OnClearAllClicked);
            if (_closeButton != null) _closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        }

        private void OnEnable()
        {
            if (_placement != null) _placement.PlacementChanged += Render;
            if (_inventory != null) _inventory.Changed += OnInventoryChanged;
            Render();
        }

        private void OnDisable()
        {
            if (_placement != null) _placement.PlacementChanged -= Render;
            if (_inventory != null) _inventory.Changed -= OnInventoryChanged;
        }

        private void OnDestroy()
        {
            _cts.Cancel();
            _cts.Dispose();
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
            if (_summaryLabel == null) return;

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
                var color = multiplier < 1f ? _negativeColor : _positiveColor;
                var hex = ColorUtility.ToHtmlStringRGB(color);
                sb.AppendLine($"  {genre,-12} <color=#{hex}>×{multiplier:0.00}</color>");

                // Soft cap heuristic — if raw product would exceed SoftCapMax we'd have clamped to it.
                if (multiplier >= Services.ConfigBasedDecorModifierProvider.SoftCapMax - 0.0001f)
                    anyCap = true;
            }

            if (!anyEffect) sb.AppendLine("  (no active decor effects)");
            _summaryLabel.text = sb.ToString();

            if (_capHintLabel != null)
            {
                if (anyCap)
                {
                    _capHintLabel.text = $"Soft cap reached on at least one genre (×{Services.ConfigBasedDecorModifierProvider.SoftCapMax:0.0}).";
                    _capHintLabel.color = _capHintColor;
                    _capHintLabel.gameObject.SetActive(true);
                }
                else
                {
                    _capHintLabel.gameObject.SetActive(false);
                }
            }
        }

        private void RenderSlots()
        {
            ClearRows(_slotRowPool);
            var location = _configs.Get<LocationConfig>(Services.DecorPlacementService.HardcodedLocationId);
            if (location?.DecorSlots == null) return;

            foreach (var slot in location.DecorSlots)
            {
                if (slot == null) continue;
                var row = SpawnSlotRow();
                var decorId = _placement.GetDecorInSlot(slot.Id);
                var decorConfig = string.IsNullOrEmpty(decorId) ? null : _configs.Get<DecorConfig>(decorId);
                row.Bind(slot, decorConfig, _positiveColor, _negativeColor);
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
                row.Bind(config, placedSlotId, _positiveColor, _negativeColor);
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
            if (_slotRowTemplate == null || _slotListRoot == null) return null;
            var row = Object.Instantiate(_slotRowTemplate, _slotListRoot);
            _slotRowPool.Add(row);
            return row;
        }

        private DecorInventoryRowView SpawnInventoryRow()
        {
            if (_inventoryRowTemplate == null || _inventoryListRoot == null) return null;
            var row = Object.Instantiate(_inventoryRowTemplate, _inventoryListRoot);
            _inventoryRowPool.Add(row);
            return row;
        }

        private void ClearRows<T>(List<T> pool) where T : MonoBehaviour
        {
            for (var i = 0; i < pool.Count; i++)
                if (pool[i] != null) Destroy(pool[i].gameObject);
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
                Debug.Log("[DecorPlacementScreen] No compatible unplaced decor available for this slot.");
                return;
            }

            if (HasNegativeEffect(candidate))
            {
                var args = new ConfirmDialogArgs(
                    title: $"Place {candidate.DisplayName}?",
                    body: BuildNegativeWarning(candidate),
                    confirmLabel: "Place anyway",
                    cancelLabel: "Cancel");

                var dialog = await _uiManager.ShowAsync<ConfirmDialog>(args, _cts.Token);
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
            var dialog = await _uiManager.ShowAsync<ConfirmDialog>(args, _cts.Token);
            if (dialog == null) return;
            var result = await dialog.WaitForResultAsync<ConfirmDialogResult>(_cts.Token);
            if (result != ConfirmDialogResult.Confirmed) return;
            await _placement.ClearAllAsync(_cts.Token);
        }
    }
}
