using VContainer;

namespace Game.Bootstrap
{
    // MonoInstaller — assign this component to GameplayLifetimeScope._monoInstallers.
    // Registers scene-specific services. Can resolve global services from parent scope.
    public sealed class GameInstaller : MonoInstaller
    {
        public override void InstallBindings(IContainerBuilder builder)
        {
            builder.RegisterDayCycle();
            builder.RegisterResources();
            builder.RegisterInventory();
            builder.RegisterShop();
            builder.RegisterRewardDrop();
            builder.RegisterIap();
            builder.RegisterQuest();
            builder.RegisterBookSell();
        }
    }
}
