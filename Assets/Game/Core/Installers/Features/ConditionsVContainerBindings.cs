using Game.Conditions.API;
using Game.Conditions.Services;
using VContainer;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope) — domain-agnostic condition engine.
    // The registry receives every IConditionFactory registered anywhere in the container (same
    // IReadOnlyList<T> injection VContainer does for InventoryUseRouter's handlers), so feature
    // bindings can contribute leaf factories without touching the engine.
    public static class ConditionsVContainerBindings
    {
        public static void RegisterConditions(this IContainerBuilder builder)
        {
            builder.Register<IConditionFactoryRegistry, ConditionFactoryRegistry>(Lifetime.Singleton);
            builder.Register<IConditionParser, ConditionParser>(Lifetime.Singleton);
        }
    }
}
