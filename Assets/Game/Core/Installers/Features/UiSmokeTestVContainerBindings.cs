using Game.UI.SmokeTest;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    // TODO: remove after Phase 0 verification along with SmokeRunner and friends.
    public static class UiSmokeTestVContainerBindings
    {
        public static void RegisterUiSmokeTest(this IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<SmokeRunner>(Lifetime.Singleton);
        }
    }
}
