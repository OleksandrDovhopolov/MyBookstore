using System;
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
        private bool _started;

        public TutorialReevaluationBridge(
            ITutorialReevaluationGate gate,
            ISalesStatsService sales = null,
            IInventoryService inventory = null,
            IDecorPlacementService decor = null)
        {
            _gate = gate ?? throw new ArgumentNullException(nameof(gate));
            _sales = sales;
            _inventory = inventory;
            _decor = decor;
        }

        public void Start()
        {
            if (_started) return;

            if (_sales != null) _sales.Changed += OnSalesChanged;
            if (_inventory != null) _inventory.Changed += OnInventoryChanged;
            if (_decor != null) _decor.PlacementChanged += OnPlacementChanged;
            _started = true;
        }

        public void Dispose()
        {
            if (!_started) return;

            if (_sales != null) _sales.Changed -= OnSalesChanged;
            if (_inventory != null) _inventory.Changed -= OnInventoryChanged;
            if (_decor != null) _decor.PlacementChanged -= OnPlacementChanged;
            _started = false;
        }

        private void OnSalesChanged(SalesStatsChange _) => _gate.RequestReevaluation();
        private void OnInventoryChanged(InventoryChangeEvent _) => _gate.RequestReevaluation();
        private void OnPlacementChanged() => _gate.RequestReevaluation();
    }
}
