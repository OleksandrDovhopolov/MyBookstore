using Book.Sell.UI.Customer;
using UnityEngine;
using VContainer;

namespace Game.Bootstrap
{
    // MonoInstaller — assign this component to GameplayLifetimeScope._monoInstallers.
    // Registers scene-specific services. Can resolve global services from parent scope.
    public sealed class GameInstaller : MonoInstaller
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

        public override void InstallBindings(IContainerBuilder builder)
        {
            builder.RegisterDayCycle();
            builder.RegisterRewardDrop();
            builder.RegisterIap();
            builder.RegisterQuest();
            builder.RegisterPreparation();
            builder.RegisterBookSell(
                _customerVisualPrefab,
                _customerSpawnRoot,
                _customerEntryLeft,
                _customerEntryRight,
                _customerShopApproach,
                _customerLaneAnchors,
                _customerExitLeft,
                _customerExitRight);
        }
    }
}
