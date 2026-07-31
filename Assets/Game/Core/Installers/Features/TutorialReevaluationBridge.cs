using System;
using System.Collections.Generic;
using Game.Conditions.API;
using Game.Decor;
using Game.Inventory.API;
using Game.SalesStats.API;
using Game.Tutorial.API;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Bridges domain state changes into the tutorial engine's pull-based eligibility scan.
    /// </summary>
    public sealed class TutorialReevaluationBridge : IStartable, IDisposable
    {
        private readonly ITutorialReevaluationGate _gate;
        private readonly ISalesStatsService _sales;
        private readonly IInventoryService _inventory;
        private readonly IDecorPlacementService _decor;
        private readonly IReadOnlyList<IConditionFactory> _factories;
        private readonly List<IConditionChangeSource> _subscribedConditionSources = new();
        private bool _started;

        public TutorialReevaluationBridge(
            ITutorialReevaluationGate gate,
            ISalesStatsService sales = null,
            IInventoryService inventory = null,
            IDecorPlacementService decor = null,
            IReadOnlyList<IConditionFactory> factories = null)
        {
            _gate = gate ?? throw new ArgumentNullException(nameof(gate));
            _sales = sales;
            _inventory = inventory;
            _decor = decor;
            _factories = factories;
        }

        public void Start()
        {
            if (_started) return;

            if (_sales != null) _sales.Changed += OnSalesChanged;
            if (_inventory != null) _inventory.Changed += OnInventoryChanged;
            if (_decor != null) _decor.PlacementChanged += OnPlacementChanged;
            SubscribeConditionChangeSources();
            _started = true;
        }

        public void Dispose()
        {
            if (!_started) return;

            if (_sales != null) _sales.Changed -= OnSalesChanged;
            if (_inventory != null) _inventory.Changed -= OnInventoryChanged;
            if (_decor != null) _decor.PlacementChanged -= OnPlacementChanged;
            UnsubscribeConditionChangeSources();
            _started = false;
        }

        private void SubscribeConditionChangeSources()
        {
            if (_factories == null) return;

            foreach (var factory in _factories)
            {
                if (factory is not IConditionChangeSource source) continue;
                if (_subscribedConditionSources.Contains(source)) continue;

                source.Changed += OnConditionSourceChanged;
                _subscribedConditionSources.Add(source);
            }
        }

        private void UnsubscribeConditionChangeSources()
        {
            for (var i = 0; i < _subscribedConditionSources.Count; i++)
                _subscribedConditionSources[i].Changed -= OnConditionSourceChanged;

            _subscribedConditionSources.Clear();
        }

        private void OnSalesChanged(SalesStatsChange _) => _gate.RequestReevaluation();
        private void OnInventoryChanged(InventoryChangeEvent _) => _gate.RequestReevaluation();
        private void OnPlacementChanged() => _gate.RequestReevaluation();
        private void OnConditionSourceChanged() => _gate.RequestReevaluation();
    }
}
