using Book.Sell.Services;
using Book.Sell.UI.Customer;
using Game.Preparation.Services;
using UnityEngine;
using VContainer;

namespace Game.Bootstrap
{
    // MonoInstaller — assign this component to LocationLifetimeScope._monoInstallers (LocationScene).
    // Registers location-scope services: the Sales day (BookSell), customer simulation/visuals and
    // the day setup provider. Resolves global services (Configs, Save, Decor) from the parent scope
    // (LocationLifetimeScope is parented to GlobalLifetimeScope by GameFlowService). See docs/GameFlowLoop.md.
    public sealed class LocationInstaller : MonoInstaller
    {
        [Header("BookSell — World HUD Phase 0")]
        [Tooltip("Placeholder prefab for visualizing customer POCO in the scene. Required for CustomerThoughtBubble.")]
        [SerializeField] private CustomerVisual _customerVisualPrefab;
        [Tooltip("Optional parent Transform under which customer visuals are spawned. Leave empty to spawn at scene root.")]
        [SerializeField] private Transform _customerSpawnRoot;
        [SerializeField] private Transform _customerEntryLeft;
        [SerializeField] private Transform _customerEntryRight;
        [SerializeField] private Transform _customerShopApproach;
        [SerializeField] private Transform[] _customerLaneAnchors;
        [SerializeField] private Transform _customerExitLeft;
        [SerializeField] private Transform _customerExitRight;

        [Header("BookSell — Tuning")]
        [Tooltip("Sales timing/pacing asset. Leave empty to use code defaults.")]
        [SerializeField] private SalesTuningConfig _salesTuningConfig;

        public override void InstallBindings(IContainerBuilder builder)
        {
            // ISalesSetupProvider читает выбор игрока из save-модуля preparation.session (его пишет хаб).
            // Раньше регистрировался в RegisterPreparation (хаб-скоп); теперь живёт в location-скопе,
            // т.к. единственный потребитель — SalesDayController (BookSell), который тоже здесь.
            builder.Register<ISalesSetupProvider, PreparationSalesSetupProvider>(Lifetime.Singleton);

            builder.RegisterBookSell(
                _customerVisualPrefab,
                _customerSpawnRoot,
                _customerEntryLeft,
                _customerEntryRight,
                _customerShopApproach,
                _customerLaneAnchors,
                _customerExitLeft,
                _customerExitRight,
                _salesTuningConfig);
        }
    }
}
