using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using Game.LocationEntry.API;
using Game.LocationUnlock.API;
using Game.Resources.API;
using Game.UI;
using Game.UI.ContentWidget;
using SpriteService;
using UnityEngine;
using VContainer;

namespace Game.Location.UI
{
    [Window("LocationWindow", WindowType.Page)]
    public sealed class LocationWindow : WindowController<LocationWindowView>, IResultWindow<string>
    {
        private IConfigsService _configs;
        private ILocationUnlockService _unlock;
        private ILocationEntryCostCalculator _entryCost;
        private IResourcesService _resources;
        private IInventoryService _inventory;
        private IUiSpriteProvider _uiSprites;

        private readonly List<LocationListItemModel> _models = new();
        private int _scrollVersion;

        public string Result { get; private set; }

        [Inject]
        public void InjectServices(
            IConfigsService configs,
            ILocationUnlockService unlock,
            ILocationEntryCostCalculator entryCost = null,
            IResourcesService resources = null,
            IInventoryService inventory = null,
            IUiSpriteProvider uiSprites = null)
        {
            _configs = configs;
            _unlock = unlock;
            _entryCost = entryCost;
            _resources = resources;
            _inventory = inventory;
            _uiSprites = uiSprites;
        }

        protected override void OnInit()
        {
        }

        protected override void OnShowStart()
        {
            Result = null;

            if (_unlock != null)
            {
                _unlock.Unlocked += OnUnlockChanged;
                _unlock.StatusChanged += OnUnlockChanged;
            }

            if (_resources != null) _resources.Changed += OnResourcesChanged;
            if (_inventory != null) _inventory.Changed += OnInventoryChanged;

            Render();
        }

        protected override void OnHideStart(bool isClosed)
        {
            if (_unlock != null)
            {
                _unlock.Unlocked -= OnUnlockChanged;
                _unlock.StatusChanged -= OnUnlockChanged;
            }

            if (_resources != null) _resources.Changed -= OnResourcesChanged;
            if (_inventory != null) _inventory.Changed -= OnInventoryChanged;
        }

        protected override void OnDispose() => View.Clear();

        private void OnUnlockChanged(string _) => Render();

        private void OnResourcesChanged(ResourceChangeEvent _) => Render();

        private void OnInventoryChanged(InventoryChangeEvent _) => Render();

        private void Render()
        {
            _models.Clear();

            if (_configs == null)
            {
                Debug.LogWarning("[LocationWindow] missing configs service; nothing to render.");
                View.Clear();
                return;
            }

            foreach (var config in _configs.GetAll<LocationConfig>())
            {
                if (config == null || string.IsNullOrEmpty(config.Id)) continue;

                var status = _unlock?.GetStatus(config.Id);
                var cost = _entryCost != null
                    ? _entryCost.Calculate(config.Id)
                    : new LocationEntryCost(ResourceIds.Gold, config.EntryCost, config.EntryCost, 0);
                var canAfford = _resources == null || _resources.Has(cost.CurrencyId, cost.Total);
                _models.Add(LocationListItemModel.From(config, status, cost, canAfford,
                    _unlock?.GetCost(config.Id)));
            }

            View.Render(_models, OnStartClicked, OnUnlockClicked, OnDemandInfoClicked, OnLocationsScrolled, _uiSprites);
        }

        private void OnStartClicked(string locationId)
        {
            Result = locationId;
            CloseAsync().Forget();
        }

        private void OnUnlockClicked(string locationId)
            => UnlockAsync(locationId).Forget();

        private void OnDemandInfoClicked(string locationId, RectTransform anchor)
            => ShowDemandWidgetAsync(locationId, anchor).Forget();

        private void OnLocationsScrolled()
        {
            _scrollVersion++;

            if (UIManager == null || !UIManager.IsWindowShown<ContentWidgetController>())
                return;

            UIManager.HideAsync<ContentWidgetController>(forceClose: true, ct: CancellationToken.None).Forget();
        }

        private async UniTaskVoid ShowDemandWidgetAsync(string locationId, RectTransform anchor)
        {
            if (anchor == null || string.IsNullOrEmpty(locationId) || _configs == null)
                return;

            try
            {
                var scrollVersion = _scrollVersion;
                var ct = View != null ? View.destroyCancellationToken : CancellationToken.None;
                if (!_configs.TryGet<LocationConfig>(locationId, out var config) || config == null)
                    return;

                var genres = new List<LocationDemandGenre>();
                var rawGenres = config.DemandGenres ?? Array.Empty<string>();
                for (var i = 0; i < rawGenres.Length; i++)
                {
                    if (!BookGenreExtensions.TryParseGenre(rawGenres[i], out var genre))
                        continue;

                    Sprite icon = null;
                    if (_uiSprites != null)
                        icon = await _uiSprites.GetSpriteAsync(genre.ToConfigValue(), ct);

                    if (ct.IsCancellationRequested || anchor == null)
                        return;

                    genres.Add(new LocationDemandGenre(genre, icon));
                }

                if (genres.Count == 0)
                    return;

                if (scrollVersion != _scrollVersion)
                    return;

                var data = new LocationDemandWidgetData(config.DisplayName, genres);
                await UIManager.ShowAsync<ContentWidgetController>(
                    new ContentWidgetArgs(
                        data,
                        anchor,
                        this,
                        placementMode: ContentWidgetPlacementMode.HorizontalOnly),
                    ct);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async UniTaskVoid UnlockAsync(string locationId)
        {
            if (_unlock == null || string.IsNullOrEmpty(locationId)) return;
            await _unlock.TryUnlockAsync(locationId, default);
            Render();
        }
    }
}
