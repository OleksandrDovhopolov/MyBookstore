using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Decor.Services;
using Game.Inventory.API;
using Game.UI;
using Game.UI.ContentWidget;
using Infrastructure.Audio;
using SpriteService;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.Decor.UI
{
    /// <summary>
    /// Visual decor placement window (MVP). Stage 1 preview flow supports both point-first and
    /// inventory-first selection, then Apply commits the preview through the placement service.
    /// The window renders committed service state and keeps preview state local/transient.
    /// </summary>
    [Window("DecorPlacementWindow", WindowType.Page)]
    public sealed class DecorPlacementWindow : WindowController<DecorPlacementWindowView>
    {
        private enum State
        {
            Default,
            PointSelected,
            DecorSelected,
            Preview,
            PlacedSlotSelected,
        }

        private IDecorPlacementService _placement;
        private IConfigsService _configs;
        private IInventoryService _inventory;
        private IUiSpriteProvider _sprites;

        private CancellationTokenSource _cts;
        private CancellationTokenSource _previewIconCts;
        private readonly Dictionary<string, string> _placedDecorBySlot = new(StringComparer.OrdinalIgnoreCase);

        private State _state = State.Default;
        private string _previewDecorId;
        private string _previewPointId;
        private string _replaceOriginalDecorId;
        private Sprite _replaceOriginalSprite;
        private string _selectedSlotId; // placed slot whose HUD is open
        private bool _applyInProgress;
        private bool _firstRender;
        private bool _useContentWidgetForInfo = false;

        // Slot-first inventory filter: when set, RenderInventory shows only decor of this PositionType.
        // Stage 1 keeps the filter type-only by design.
        private DecorPositionType? _slotTypeFilter;

        [Inject]
        public void InjectServices(
            IDecorPlacementService placement,
            IConfigsService configs,
            IInventoryService inventory,
            IUiSpriteProvider sprites)
        {
            _placement = placement;
            _configs = configs;
            _inventory = inventory;
            _sprites = sprites;
        }

        protected override void OnInit()
        {
            _cts = new CancellationTokenSource();

            if (View.SelectedSlotHud != null) View.SelectedSlotHud.SetActive(false);
            if (View.PreviewActionsRoot != null) View.PreviewActionsRoot.SetActive(false);
            SetButtonVisible(View.ReplaceButton, false, false);

            if (View.RemoveButton != null) View.RemoveButton.onClick.AddListener(OnRemoveClicked);
            if (View.CancelPreviewButton != null) View.CancelPreviewButton.onClick.AddListener(OnCancelClicked);
            if (View.ApplyPreviewButton != null) View.ApplyPreviewButton.onClick.AddListener(OnApplyClicked);
            if (View.HudBackdrop != null) View.HudBackdrop.onClick.AddListener(OnBackdropClicked);

            // Anchors are authored in the prefab and live as long as the view — subscribe once.
            if (View.SlotAnchors != null)
            {
                foreach (var anchor in View.SlotAnchors)
                {
                    if (anchor == null) continue;
                    var captured = anchor;
                    captured.OnMarkerClicked += () => OnMarkerClicked(captured);
                    captured.OnPlacedClicked += () => OnPlacedClicked(captured);
                }
            }
        }

        protected override void OnShowStart()
        {
            if (_placement != null) _placement.PlacementChanged += Render;
            if (_inventory != null) _inventory.Changed += OnInventoryChanged;

            // Clean, non-animated re-sync on every open.
            _placedDecorBySlot.Clear();
            CancelPreviewIconLoad();
            _slotTypeFilter = null;
            _previewDecorId = null;
            _previewPointId = null;
            _replaceOriginalDecorId = null;
            _replaceOriginalSprite = null;
            _selectedSlotId = null;
            _applyInProgress = false;
            _state = State.Default;
            _firstRender = true;
            Render();
        }

        protected override void OnHideStart(bool isClosed)
        {
            if (_placement != null) _placement.PlacementChanged -= Render;
            if (_inventory != null) _inventory.Changed -= OnInventoryChanged;

            CancelPreview();
        }

        protected override void OnDispose()
        {
            CancelPreviewIconLoad();

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (View == null) return;
            if (View.RemoveButton != null) View.RemoveButton.onClick.RemoveListener(OnRemoveClicked);
            if (View.CancelPreviewButton != null) View.CancelPreviewButton.onClick.RemoveListener(OnCancelClicked);
            if (View.ApplyPreviewButton != null) View.ApplyPreviewButton.onClick.RemoveListener(OnApplyClicked);
            if (View.HudBackdrop != null) View.HudBackdrop.onClick.RemoveListener(OnBackdropClicked);
        }

        private void OnInventoryChanged(InventoryChangeEvent _) => Render();

        private void Render()
        {
            if (_placement == null || View == null) return;
            RenderSlots();
            ResetTransientToCommitted();
            RenderInventory();
            _firstRender = false;
        }

        private void RenderSlots()
        {
            if (View.SlotAnchors == null) return;

            foreach (var anchor in View.SlotAnchors)
            {
                if (anchor == null) continue;
                var slotId = anchor.SlotId;
                var decorId = _placement.GetDecorInSlot(slotId);
                var nowPlaced = !string.IsNullOrEmpty(decorId);
                var wasPlaced = _placedDecorBySlot.TryGetValue(slotId, out var previousDecorId);

                if (nowPlaced && !wasPlaced)
                {
                    _placedDecorBySlot[slotId] = decorId;
                    LoadPlacedAsync(anchor, decorId, animate: !_firstRender, _cts.Token).Forget();
                }
                else if (nowPlaced && wasPlaced && !string.Equals(previousDecorId, decorId, StringComparison.OrdinalIgnoreCase))
                {
                    _placedDecorBySlot[slotId] = decorId;
                    LoadPlacedAsync(anchor, decorId, animate: false, _cts.Token).Forget();
                }
                else if (!nowPlaced && wasPlaced)
                {
                    _placedDecorBySlot.Remove(slotId);
                    if (_firstRender) anchor.SetEmpty();
                    else anchor.PlayRemoveTween(() => { if (anchor != null) anchor.SetEmpty(); });
                }
                else if (!nowPlaced && !wasPlaced && _firstRender)
                {
                    anchor.SetEmpty();
                }
                // nowPlaced && wasPlaced && same decor: already shown, leave as is.
            }
        }

        private async UniTaskVoid LoadPlacedAsync(DecorSlotAnchorView anchor, string decorId, bool animate, CancellationToken ct)
        {
            Sprite sprite = null;
            if (_sprites != null)
            {
                try { sprite = await _sprites.GetSpriteAsync(decorId, ct); }
                catch (System.OperationCanceledException) { return; }
            }
            if (ct.IsCancellationRequested || anchor == null) return;

            anchor.SetPlaced(sprite);
            if (animate) anchor.PlayPlaceTween();
        }

        private void RenderInventory()
        {
            var pool = View.CardsPool;
            if (pool == null) return;

            pool.DisableAll();
            var selectable = true;
            var items = _inventory.GetByCategory(InventoryCategories.Decor);
            foreach (var item in items)
            {
                var config = _configs.Get<DecorConfig>(item.ItemId);
                if (config == null) continue;
                // Slot-first filter: hide decor that doesn't match the clicked slot's type. Checked
                // before GetNext() so a hidden card never consumes a pooled view.
                if (_slotTypeFilter.HasValue && config.PositionType != _slotTypeFilter.Value) continue;
                var placed = !string.IsNullOrEmpty(FindPlacedSlot(item.ItemId));
                var card = pool.GetNext();
                card.Bind(config, placed, selectable, _sprites, OnCardSelect, OnCardInfo);
            }
            pool.DisableNonActive();
        }

        private void OnCardSelect(string decorId)
        {
            if (IsReplaceContextActive())
            {
                EnterReplacePreview(decorId);
                return;
            }

            HideHud();

            _previewDecorId = decorId;
            _applyInProgress = false;

            SelectInventoryCard(decorId);

            if (!string.IsNullOrEmpty(_previewPointId))
            {
                if (!IsAnchorCompatibleWithDecor(_previewPointId, decorId))
                {
                    ClearPreviewVisualIfStillEmpty();
                    _previewPointId = null;
                    ApplyDecorAvailabilityFilter(decorId);
                    _state = State.DecorSelected;
                    HidePreviewActions();
                    return;
                }

                ApplySelectedPointAvailability(_previewPointId);
                _state = State.Preview;
                ShowPreviewActions();
                SetApplyInteractable(true);
                LoadPreviewSpriteAsync(decorId, _previewPointId).Forget();
            }
            else
            {
                ApplyDecorAvailabilityFilter(decorId);
                _state = State.DecorSelected;
                HidePreviewActions();
            }
        }

        // Info is shown as an anchored widget by default. The popup branch stays available so the
        // caller can switch presentation without deleting DecorInfoPopup.
        private void OnCardInfo(string decorId, RectTransform anchor)
        {
            if (_useContentWidgetForInfo)
            {
                ShowDecorInfoWidgetAsync(decorId, anchor).Forget();
                return;
            }

            UIManager.ShowAsync<DecorInfoPopup>(new DecorInfoPopupArgs(decorId), _cts.Token).Forget();
        }

        private async UniTaskVoid ShowDecorInfoWidgetAsync(string decorId, RectTransform anchor)
        {
            if (anchor == null || string.IsNullOrEmpty(decorId)) return;

            try
            {
                var data = await BuildDecorInfoWidgetDataAsync(decorId, _cts.Token);
                if (data == null) return;

                await UIManager.ShowAsync<ContentWidgetController>(new ContentWidgetArgs(data, anchor, this), _cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async UniTask<DecorInfoWidgetData> BuildDecorInfoWidgetDataAsync(string decorId, CancellationToken ct)
        {
            var config = _configs.Get<DecorConfig>(decorId);
            if (config == null) return null;

            var iconTask = _sprites != null
                ? _sprites.GetSpriteAsync(decorId, ct)
                : UniTask.FromResult<Sprite>(null);
            var bonusDrafts = new List<(string Genre, string PercentText, Color Color)>();
            var bonusSpriteTasks = new List<UniTask<Sprite>>();
            if (config.GenreMultipliers != null)
            {
                foreach (var mod in config.GenreMultipliers)
                {
                    if (mod == null) continue;

                    var percent = Mathf.RoundToInt((mod.Multiplier - 1f) * 100f);
                    var sign = percent >= 0 ? "+" : "";
                    var color = mod.Multiplier < 1f
                        ? new Color(0.9f, 0.25f, 0.25f)
                        : new Color(0.2f, 0.8f, 0.2f);
                    bonusDrafts.Add((
                        mod.Genre,
                        $"{sign}{percent}%",
                        color));
                    bonusSpriteTasks.Add(_sprites != null
                        ? _sprites.GetSpriteAsync(mod.Genre, ct)
                        : UniTask.FromResult<Sprite>(null));
                }
            }

            var icon = await iconTask;
            var bonusSprites = await UniTask.WhenAll(bonusSpriteTasks);
            var bonuses = new List<DecorInfoWidgetData.BonusRow>(bonusDrafts.Count);
            for (var i = 0; i < bonusDrafts.Count; i++)
            {
                var draft = bonusDrafts[i];
                bonuses.Add(new DecorInfoWidgetData.BonusRow(
                    draft.Genre,
                    draft.PercentText,
                    draft.Color,
                    i < bonusSprites.Length ? bonusSprites[i] : null));
            }

            return new DecorInfoWidgetData(
                config.DisplayName ?? config.Id,
                "TODO: item description. Add a Description field to DecorConfig and pass it here.",
                icon,
                bonuses,
                BuildDecorCharacteristics(config));
        }

        private static IReadOnlyList<string> BuildDecorCharacteristics(DecorConfig config)
        {
            var result = new List<string>
            {
                config.PositionType.ToString(),
                config.Size.ToString()
            };

            if (config.AtmosphereTags != null)
                foreach (var tag in config.AtmosphereTags)
                    if (!string.IsNullOrEmpty(tag))
                        result.Add(tag);

            return result;
        }

        private bool IsReplaceContextActive() =>
            !string.IsNullOrEmpty(_replaceOriginalDecorId)
            && !string.IsNullOrEmpty(_previewPointId)
            && (_state == State.PlacedSlotSelected || _state == State.Preview);

        private void EnterReplacePreview(string decorId)
        {
            if (!IsAnchorCompatibleWithDecor(_previewPointId, decorId)) return;

            _previewDecorId = decorId;
            _applyInProgress = false;
            _state = State.Preview;

            SelectInventoryCard(decorId);
            ApplySelectedPointAvailability(_previewPointId);
            SetButtonVisible(View.RemoveButton, false, false);
            ShowPreviewActions();
            SetApplyInteractable(true);
            LoadPreviewSpriteAsync(decorId, _previewPointId).Forget();
        }

        // ── Placed-slot HUD (Remove; replacement starts from the filtered inventory) ──────────
        private void OnPlacedClicked(DecorSlotAnchorView anchor)
        {
            if (anchor == null) return;
            var originalDecorId = _placement.GetDecorInSlot(anchor.SlotId);
            if (string.IsNullOrEmpty(originalDecorId)) return;

            CancelPreview();
            _previewPointId = anchor.SlotId;
            _replaceOriginalDecorId = originalDecorId;
            _replaceOriginalSprite = anchor.CurrentPlacedSprite;
            _previewDecorId = null;
            _applyInProgress = false;

            ShowOccupiedHud(anchor);
            TrySetSlotFilter(anchor.SlotId);
            ApplySelectedPointAvailability(anchor.SlotId);
        }

        private void ShowOccupiedHud(DecorSlotAnchorView anchor)
        {
            if (View.SelectedSlotHud == null) return;

            if (View.SelectedSlotHud.transform is RectTransform hudRect)
            {
                //hudRect.position = anchor.transform.position; // park the HUD next to the slot
            }

            View.SelectedSlotHud.SetActive(true);
            HidePreviewActions();
            SetSelectedDecorInfoVisible(true);
            SetButtonVisible(View.ReplaceButton, false, false);
            SetButtonVisible(View.RemoveButton, true, true);

            anchor.SetSelectedOutline(true);
            BindSelectedDecorInfo(anchor.SlotId);
            _selectedSlotId = anchor.SlotId;
            _state = State.PlacedSlotSelected;
        }

        // Shows the clicked decor's name + sprite in the side panel. Sprite comes from the shared
        // (cached) provider by decor id, so this is a cheap re-fetch of the already-loaded sprite.
        private void BindSelectedDecorInfo(string slotId)
        {
            var decorId = _placement.GetDecorInSlot(slotId);
            var config = string.IsNullOrEmpty(decorId) ? null : _configs.Get<DecorConfig>(decorId);

            if (View.SelectedDecorNameLabel != null)
                View.SelectedDecorNameLabel.text = config != null ? config.DisplayName ?? config.Id : string.Empty;

            if (View.SelectedDecorImage != null)
            {
                View.SelectedDecorImage.sprite = null;
                if (!string.IsNullOrEmpty(decorId))
                    LoadSelectedDecorImageAsync(decorId, _cts.Token).Forget();
            }
        }

        private async UniTaskVoid LoadSelectedDecorImageAsync(string decorId, CancellationToken ct)
        {
            if (_sprites == null || View == null || View.SelectedDecorImage == null) return;

            Sprite sprite;
            try { sprite = await _sprites.GetSpriteAsync(decorId, ct); }
            catch (System.OperationCanceledException) { return; }

            if (ct.IsCancellationRequested || View == null || View.SelectedDecorImage == null) return;
            View.SelectedDecorImage.sprite = sprite;
        }

        private void HideHud()
        {
            if (View != null && View.SelectedSlotHud != null) View.SelectedSlotHud.SetActive(false);

            if (View != null)
            {
                SetButtonVisible(View.ReplaceButton, false, false);
                SetButtonVisible(View.RemoveButton, false, false);
                SetSelectedDecorInfoVisible(false);

                if (View.SelectedDecorNameLabel != null) View.SelectedDecorNameLabel.text = string.Empty;
                if (View.SelectedDecorImage != null) View.SelectedDecorImage.sprite = null;
            }

            if (!string.IsNullOrEmpty(_selectedSlotId) && View != null && View.SlotAnchors != null)
                foreach (var anchor in View.SlotAnchors)
                    if (anchor != null && anchor.SlotId == _selectedSlotId) anchor.SetSelectedOutline(false);

            _selectedSlotId = null;
            if (_state == State.PlacedSlotSelected) _state = State.Default;
        }

        private static void SetButtonVisible(Button button, bool visible, bool interactable)
        {
            if (button == null) return;
            button.gameObject.SetActive(visible);
            button.interactable = interactable;
        }

        private void SetApplyInteractable(bool interactable)
        {
            if (View?.ApplyPreviewButton != null) View.ApplyPreviewButton.interactable = interactable;
        }

        private void ShowPreviewActions()
        {
            if (View?.PreviewActionsRoot != null) View.PreviewActionsRoot.SetActive(true);
            SetButtonVisible(View?.CancelPreviewButton, true, true);
            SetButtonVisible(View?.ApplyPreviewButton, true, !string.IsNullOrEmpty(_previewDecorId) && !_applyInProgress);
        }

        private void HidePreviewActions()
        {
            if (View?.PreviewActionsRoot != null) View.PreviewActionsRoot.SetActive(false);
            SetButtonVisible(View?.CancelPreviewButton, false, false);
            SetButtonVisible(View?.ApplyPreviewButton, false, false);
        }

        private void SetSelectedDecorInfoVisible(bool visible)
        {
            if (View == null) return;
            if (View.SelectedDecorNameLabel != null) View.SelectedDecorNameLabel.gameObject.SetActive(visible);
            if (View.SelectedDecorImage != null) View.SelectedDecorImage.gameObject.SetActive(visible);
        }

        // The full-screen backdrop is the reset point for preview, filters, dimming, and tools.
        private void OnBackdropClicked() => CancelPreview();

        // Slot-first filter: show only inventory decor of the clicked slot's PositionType. Null-safe
        // against a prefab/config mismatch; re-renders the inventory to apply immediately.
        private void TrySetSlotFilter(string slotId)
        {
            if (!BuildSlotMap().TryGetValue(slotId, out var slot) || slot == null) return;
            _slotTypeFilter = slot.PositionType;
            RenderInventory();
        }

        private void OnRemoveClicked()
        {
            if (string.IsNullOrEmpty(_selectedSlotId)) return;
            var slotId = _selectedSlotId;
            HideHud();
            RemoveAsync(slotId).Forget();
        }

        private async UniTaskVoid RemoveAsync(string slotId)
        {
            try
            {
                // Visual (remove tween → SetEmpty) is handled by PlacementChanged → Render diff.
                await _placement.UnplaceAsync(slotId, _cts.Token);
                PlayUi(View != null ? View.RemoveClip : null);
            }
            catch (System.OperationCanceledException) { }
        }

        // "All available" state: empty markers are interactable; occupied slots keep their placed
        // button behavior but lose any temporary dim/selection state.
        private void ResetSlotsAvailability()
        {
            if (View.SlotAnchors == null) return;

            foreach (var anchor in View.SlotAnchors)
            {
                if (anchor == null) continue;

                var occupied = !string.IsNullOrEmpty(_placement.GetDecorInSlot(anchor.SlotId));
                anchor.SetAvailabilityVisual(true);
                if (!occupied) anchor.SetMarkerInteractable(true);
                anchor.SetHighlighted(false);
                anchor.SetSelectedOutline(false);
            }
        }

        private void ApplySelectedPointAvailability(string selectedSlotId)
        {
            if (View.SlotAnchors == null) return;

            foreach (var anchor in View.SlotAnchors)
            {
                if (anchor == null) continue;

                var selected = string.Equals(anchor.SlotId, selectedSlotId, StringComparison.OrdinalIgnoreCase);
                var occupied = !string.IsNullOrEmpty(_placement.GetDecorInSlot(anchor.SlotId));
                anchor.SetAvailabilityVisual(selected);
                if (!occupied) anchor.SetMarkerInteractable(selected);
                anchor.SetHighlighted(false);
                anchor.SetSelectedOutline(selected);
            }
        }

        private void ApplyDecorAvailabilityFilter(string decorId)
        {
            if (View.SlotAnchors == null) return;

            foreach (var anchor in View.SlotAnchors)
            {
                if (anchor == null) continue;

                var occupied = !string.IsNullOrEmpty(_placement.GetDecorInSlot(anchor.SlotId));
                var compatible = IsAnchorCompatibleWithDecor(anchor.SlotId, decorId);
                anchor.SetAvailabilityVisual(compatible);
                if (!occupied) anchor.SetMarkerInteractable(compatible);
                anchor.SetHighlighted(!occupied && compatible);
                anchor.SetSelectedOutline(false);
            }
        }

        private bool IsAnchorCompatibleWithDecor(string slotId, string decorId)
        {
            var config = string.IsNullOrEmpty(decorId) ? null : _configs.Get<DecorConfig>(decorId);
            if (config == null) return false;
            return BuildSlotMap().TryGetValue(slotId, out var slot)
                && slot != null
                && slot.PositionType == config.PositionType;
        }

        private void OnMarkerClicked(DecorSlotAnchorView anchor)
        {
            if (anchor == null) return;

            if (!string.IsNullOrEmpty(_previewDecorId))
            {
                EnterPreviewAtPoint(anchor);
                return;
            }

            EnterEmptyPreview(anchor);
        }

        private void EnterPreviewAtPoint(DecorSlotAnchorView anchor)
        {
            if (!IsAnchorCompatibleWithDecor(anchor.SlotId, _previewDecorId)) return;

            ClearPreviewVisualIfStillEmpty();
            HideHud();

            _previewPointId = anchor.SlotId;
            _applyInProgress = false;
            _state = State.Preview;

            ApplySelectedPointAvailability(anchor.SlotId);
            anchor.SetSelectedOutline(true);
            ShowPreviewActions();
            SetApplyInteractable(true);
            LoadPreviewSpriteAsync(_previewDecorId, anchor.SlotId).Forget();
        }

        private void EnterEmptyPreview(DecorSlotAnchorView anchor)
        {
            CancelPreview();
            HideHud();

            _previewPointId = anchor.SlotId;
            _previewDecorId = null;
            _applyInProgress = false;
            _state = State.PointSelected;

            anchor.SetSelectedOutline(true);
            ApplySelectedPointAvailability(anchor.SlotId);
            TrySetSlotFilter(anchor.SlotId);
            HidePreviewActions();
        }

        private void OnCancelClicked() => CancelPreview();

        private void CancelPreview()
        {
            CancelPreviewIconLoad();
            if (!string.IsNullOrEmpty(_replaceOriginalDecorId)) RestoreReplaceOriginalVisualIfCurrent();
            else ClearPreviewVisualIfStillEmpty();

            _previewDecorId = null;
            _previewPointId = null;
            _replaceOriginalDecorId = null;
            _replaceOriginalSprite = null;
            _slotTypeFilter = null;
            _applyInProgress = false;

            DeselectCards();
            ResetSlotsAvailability();
            HideHud();
            HidePreviewActions();
            _state = State.Default;
            if (View != null) RenderInventory();
        }

        private void ResetTransientToCommitted()
        {
            CancelPreviewIconLoad();
            if (!string.IsNullOrEmpty(_replaceOriginalDecorId)) RestoreReplaceOriginalVisualIfCurrent();
            else ClearPreviewVisualIfStillEmpty();

            _previewDecorId = null;
            _previewPointId = null;
            _replaceOriginalDecorId = null;
            _replaceOriginalSprite = null;
            _slotTypeFilter = null;
            _applyInProgress = false;

            DeselectCards();
            ResetSlotsAvailability();
            HideHud();
            HidePreviewActions();
            _state = State.Default;
        }

        private void ClearPreviewVisualIfStillEmpty()
        {
            if (string.IsNullOrEmpty(_previewPointId) || View?.SlotAnchors == null || _placement == null) return;
            if (!string.IsNullOrEmpty(_placement.GetDecorInSlot(_previewPointId))) return;

            var anchor = FindAnchor(_previewPointId);
            if (anchor != null) anchor.SetEmpty();
        }

        private void RestoreReplaceOriginalVisualIfCurrent()
        {
            if (string.IsNullOrEmpty(_previewPointId) || string.IsNullOrEmpty(_replaceOriginalDecorId)) return;
            if (_placement == null || !string.Equals(_placement.GetDecorInSlot(_previewPointId), _replaceOriginalDecorId, StringComparison.OrdinalIgnoreCase)) return;

            var anchor = FindAnchor(_previewPointId);
            if (anchor != null) anchor.SetPlaced(_replaceOriginalSprite);
        }

        private async UniTaskVoid LoadPreviewSpriteAsync(string decorId, string pointId)
        {
            CancelPreviewIconLoad();

            var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            _previewIconCts = linked;
            var token = linked.Token;

            Sprite sprite = null;
            if (_sprites != null)
            {
                try { sprite = await _sprites.GetSpriteAsync(decorId, token); }
                catch (OperationCanceledException) { return; }
            }

            if (token.IsCancellationRequested || !ReferenceEquals(_previewIconCts, linked)) return;
            if (_state != State.Preview || _previewPointId != pointId || _previewDecorId != decorId) return;
            if (string.IsNullOrEmpty(_replaceOriginalDecorId) && !string.IsNullOrEmpty(_placement.GetDecorInSlot(pointId))) return;

            var anchor = FindAnchor(pointId);
            if (anchor == null) return;
            anchor.SetPreview(sprite);
        }

        private void CancelPreviewIconLoad()
        {
            if (_previewIconCts == null) return;
            _previewIconCts.Cancel();
            _previewIconCts.Dispose();
            _previewIconCts = null;
        }

        private void OnApplyClicked()
        {
            if (_applyInProgress) return;
            ApplyAsync().Forget();
        }

        private async UniTaskVoid ApplyAsync()
        {
            if (_state != State.Preview) return;
            if (string.IsNullOrEmpty(_previewDecorId) || string.IsNullOrEmpty(_previewPointId)) return;

            var decorId = _previewDecorId;
            var pointId = _previewPointId;
            var isReplace = !string.IsNullOrEmpty(_replaceOriginalDecorId);
            _applyInProgress = true;
            SetApplyInteractable(false);

            try
            {
                var result = isReplace
                    ? await _placement.ReplaceAsync(decorId, pointId, _cts.Token)
                    : await _placement.PlaceAsync(decorId, pointId, _cts.Token);
                if (result == DecorPlacementResult.Success)
                {
                    PlayUi(View != null ? View.PlaceClip : null);
                }
                else
                {
                    var operation = isReplace ? "Replace" : "Place";
                    Debug.Log($"[DecorPlacementWindow] {operation} '{decorId}' -> '{pointId}' failed: {result}");
                    RestoreApplyIfPreviewStillActive(decorId, pointId);
                }
            }
            catch (OperationCanceledException) { }
        }

        private bool PreviewMatches(string decorId, string pointId) =>
            _state == State.Preview
            && _previewDecorId == decorId
            && _previewPointId == pointId;

        private void RestoreApplyIfPreviewStillActive(string decorId, string pointId)
        {
            _applyInProgress = false;
            if (!PreviewMatches(decorId, pointId)) return;
            ShowPreviewActions();
        }

        // Null-safe: no-op if the clip is unassigned or the audio service is not bound. Assigning a
        // clip in the inspector is enough to make it play — no code change needed.
        private static void PlayUi(AudioClip clip)
        {
            if (clip != null) Audio.PlayUi(clip);
        }

        private void DeselectCards()
        {
            if (View?.CardsPool == null) return;
            foreach (var card in View.CardsPool.ActiveElements())
                if (card != null) card.SetSelected(false);
        }

        private void SelectInventoryCard(string decorId)
        {
            DeselectCards();
            if (View?.CardsPool == null) return;
            foreach (var card in View.CardsPool.ActiveElements())
                if (card != null) card.SetSelected(card.DecorId == decorId);
        }

        private Dictionary<string, DecorSlot> BuildSlotMap()
        {
            var map = new Dictionary<string, DecorSlot>();
            var shop = _configs.Get<BookShopConfig>(DecorPlacementService.HardcodedBookShopId);
            if (shop?.DecorSlots == null) return map;
            foreach (var slot in shop.DecorSlots)
                if (slot != null && !string.IsNullOrEmpty(slot.Id)) map[slot.Id] = slot;
            return map;
        }

        private DecorSlotAnchorView FindAnchor(string slotId)
        {
            if (View?.SlotAnchors == null || string.IsNullOrEmpty(slotId)) return null;
            foreach (var anchor in View.SlotAnchors)
                if (anchor != null && anchor.SlotId == slotId) return anchor;
            return null;
        }

        private string FindPlacedSlot(string decorId)
        {
            foreach (var entry in _placement.GetAllPlacements())
                if (string.Equals(entry.DecorId, decorId, StringComparison.OrdinalIgnoreCase))
                    return entry.SlotId;
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
    }
}
