using System;
using Game.Conditions.API;
using Game.Tutorial.API;
using Game.Tutorial.Conditions;
using Game.Tutorial.Content;
using Game.Tutorial.Presentation;
using Game.Tutorial.Services;
using Infrastructure.TutorialUI;
using VContainer;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope) - must be global so TutorialService's ISaveHook
    // is registered before SaveDataLoadOperation (Bootstrap.Construct force-constructs ITutorialService).
    // Resolves from the same scope: ISaveService, MessagePipe pub/sub, IUICanvasRoot, IUIManager,
    // registered ITutorialSequence content, and optional IDayProgressService, IGameFlowService,
    // IQuestsService, IQuestReevaluationGate.
    public static class TutorialVContainerBindings
    {
        public static void RegisterTutorial(
            this IContainerBuilder builder, TutorialOverlaySettings overlaySettings, bool autoStart = true)
        {
            builder.RegisterInstance(overlaySettings != null ? overlaySettings : TutorialOverlaySettings.CreateDefault());
            builder.Register<ITutorialAutoStartGate, TutorialAutoStartGate>(Lifetime.Singleton);
            builder.Register<ITutorialTargetRegistry, TutorialTargetRegistry>(Lifetime.Singleton);
            builder.Register<TutorialOverlayController>(Lifetime.Singleton);

            // Bind the registry to the static facade so scene/prefab TutorialTargetTag components (which get no
            // DI injection) can self-register. DI consumers still depend on ITutorialTargetRegistry. Mirrors Audio.
            builder.RegisterBuildCallback(resolver => TutorialTargets.Bind(resolver.Resolve<ITutorialTargetRegistry>()));

            builder.Register<TutorialDayOne>(Lifetime.Singleton).As<ITutorialSequence>();
            builder.Register<TutorialHub>(Lifetime.Singleton).As<ITutorialSequence>();

            // TutorialService self-registers as ISaveHook in its constructor; AfterLoadAsync builds the
            // catalog from registered C# tutorial content, restores state, subscribes triggers.
            builder.Register<TutorialService>(Lifetime.Singleton)
                .As<ITutorialService>()
                .WithParameter("autoStart", autoStart);

            // "tutorialCompleted" leaf. The factory holds a lazy Func<ITutorialService> so building the
            // IConditionFactory collection never forces the tutorial service - no DI cycle. The Func is
            // registered as its own service (NOT the factory via a Func-registration) because VContainer's
            // IConditionFactory collection cannot hold two Func-based entries - WeatherIsConditionFactory is
            // already one, and a second (this) collides ("Conflict implementation type"). Registering the
            // factory by concrete type gives it a distinct implementation type in the collection.
            builder.Register<Func<ITutorialService>>(
                resolver => () => resolver.Resolve<ITutorialService>(),
                Lifetime.Singleton);
            builder.Register<IConditionFactory, TutorialCompletedConditionFactory>(Lifetime.Singleton);
        }
    }
}
