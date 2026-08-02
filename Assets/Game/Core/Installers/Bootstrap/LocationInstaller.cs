using Book.Sell.Services;
using Book.Sell.UI.Customer;
using Game.Location.API;
using Game.Location.Runtime;
using Game.Preparation.Services;
using Infrastructure;
using UnityEngine;
using VContainer;
using VContainer.Unity;

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
        [SerializeField] private Transform[] _customerBubbleSlots;
        [SerializeField] private Transform _customerExitLeft;
        [SerializeField] private Transform _customerExitRight;
        [Tooltip("Parent under LocationRoot where the preloaded location prefab is mounted.")]
        [SerializeField] private Transform _locationVisualRoot;

        [Header("BookSell — Tuning")]
        [Tooltip("Sales timing/pacing asset. Leave empty to use code defaults.")]
        [SerializeField] private SalesTuningConfig _salesTuningConfig;

        [Tooltip("Customer traffic knobs (default count, min/max, rounding). Leave empty to use code defaults.")]
        [SerializeField] private SalesTrafficConfig _salesTrafficConfig;

        public override void InstallBindings(IContainerBuilder builder)
        {
            // ISalesSetupProvider читает выбор игрока из save-модуля preparation.session (его пишет хаб).
            // Раньше регистрировался в RegisterPreparation (хаб-скоп); теперь живёт в location-скопе,
            // т.к. единственный потребитель — SalesDayController (BookSell), который тоже здесь.
            builder.Register<ISalesSetupProvider, PreparationSalesSetupProvider>(Lifetime.Singleton);

            var locationContext = new LocationContextBinder(new SceneLocationContext(
                _customerSpawnRoot,
                _customerEntryLeft,
                _customerEntryRight,
                _customerShopApproach,
                _customerLaneAnchors,
                _customerExitLeft,
                _customerExitRight,
                _customerBubbleSlots));

            builder.RegisterInstance(locationContext).AsSelf().As<ILocationContext>();
            builder.RegisterBuildCallback(resolver => MountLocationPrefab(resolver, locationContext));

            builder.RegisterBookSell(
                _customerVisualPrefab,
                locationContext,
                _salesTuningConfig,
                _salesTrafficConfig);
        }

        private void MountLocationPrefab(IObjectResolver resolver, LocationContextBinder context)
        {
            if (_locationVisualRoot == null)
            {
                Debug.LogWarning("[LocationPrefab] Location visual root is not assigned. Using scene fallback anchors.");
                return;
            }

            var provider = resolver.ResolveOrDefault<ILocationPrefabProvider>();
            var locationId = provider?.LastPreloadedLocationId;
            var prefab = provider?.GetPreloaded(locationId);

#if UNITY_EDITOR
            if (prefab == null)
            {
                try
                {
                    prefab = ProdAddressablesWrapper.LoadSync<GameObject>("location/park");
                    locationId = "loc_park";
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[LocationPrefab] Editor fallback load failed: {ex.Message}. Using scene fallback anchors.");
                }
            }
#endif

            if (prefab == null)
            {
                Debug.LogWarning("[LocationPrefab] No preloaded location prefab. Using scene fallback anchors.");
                return;
            }

            var instance = resolver.Instantiate(prefab, _locationVisualRoot, false);
            instance.name = prefab.name;

            var controller = instance.GetComponent<LocationController>();
            if (controller == null)
            {
                Debug.LogWarning($"[LocationPrefab] Mounted '{prefab.name}' for '{locationId}', but it has no LocationController. Using scene fallback anchors.");
                return;
            }

            context.Bind(controller);
        }
    }
}
