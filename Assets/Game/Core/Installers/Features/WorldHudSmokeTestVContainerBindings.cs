using Game.WorldHud.SmokeTest;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    // TODO: remove after World HUD Phase 0 verification along with WorldHudSmokeRunner + SmokeWorldHudBubble.
    public static class WorldHudSmokeTestVContainerBindings
    {
        public static void RegisterWorldHudSmokeTest(this IContainerBuilder builder)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            builder.RegisterEntryPoint<WorldHudSmokeRunner>(Lifetime.Singleton);
#endif
        }
    }
}
