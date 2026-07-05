using System;
using Game.Conditions.API;
using Game.Tutorial.API;
using Game.Tutorial.Conditions;
using Game.Tutorial.Services;
using Game.Tutorial.Steps;
using VContainer;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope) — must be global so its ISaveHook is
    // registered before SaveDataLoadOperation (Bootstrap.Construct force-constructs ITutorialService).
    // Resolves from the same scope: ISaveService, IConfigsService, IConditionParser, MessagePipe pub/sub,
    // and (optional) IDayProgressService, IGameFlowService, IQuestsService, IQuestReevaluationGate.
    public static class TutorialVContainerBindings
    {
        public static void RegisterTutorial(this IContainerBuilder builder)
        {
            // Step handlers (Pass 1 stubs). Collected as IReadOnlyList<ITutorialStepHandler> by the registry.
            builder.Register<ITutorialStepHandler, ShowTextStepHandler>(Lifetime.Singleton);
            builder.Register<ITutorialStepHandler, HighlightClickStepHandler>(Lifetime.Singleton);
            builder.Register<ITutorialStepHandler, AwaitWindowStepHandler>(Lifetime.Singleton);
            builder.Register<ITutorialStepHandler, AwaitQuestStepHandler>(Lifetime.Singleton);
            builder.Register<ITutorialStepHandler, AwaitPhaseStepHandler>(Lifetime.Singleton);
            builder.Register<ITutorialStepHandler, AwaitLocationStepHandler>(Lifetime.Singleton);
            builder.Register<TutorialStepHandlerRegistry>(Lifetime.Singleton);

            // TutorialService self-registers as ISaveHook in its constructor; AfterLoadAsync builds the
            // catalog from tutorials.json (configs are warm by then), restores state, subscribes triggers.
            builder.Register<TutorialService>(Lifetime.Singleton)
                .As<ITutorialService>();

            // "tutorialCompleted" leaf. The factory holds a lazy Func<ITutorialService> so building the
            // IConditionFactory collection never forces the tutorial service → no DI cycle. The Func is
            // registered as its own service (NOT the factory via a Func-registration) because VContainer's
            // IConditionFactory collection cannot hold two Func-based entries — WeatherIsConditionFactory is
            // already one, and a second (this) collides ("Conflict implementation type"). Registering the
            // factory by concrete type gives it a distinct implementation type in the collection.
            builder.Register<Func<ITutorialService>>(
                resolver => () => resolver.Resolve<ITutorialService>(),
                Lifetime.Singleton);
            builder.Register<IConditionFactory, TutorialCompletedConditionFactory>(Lifetime.Singleton);
        }
    }
}
