using MessagePipe;
using Game.Tutorial.API;
using Game.UI;
using UIShared;
using VContainer;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope — messages must be available globally).
    // After this is called, child scopes (e.g. GameplayLifetimeScope) automatically inherit
    // the registered message brokers via VContainer parent-child resolution.
    public static class MessagePipeVContainerBindings
    {
        public static MessagePipeOptions RegisterMessagePipeBus(this IContainerBuilder builder)
        {
            var options = builder.RegisterMessagePipe(/* configure: */ c =>
            {
#if UNITY_EDITOR
                c.EnableCaptureStackTrace = true;
#endif
            });

            builder.RegisterBuildCallback(c => GlobalMessagePipe.SetProvider(c.AsServiceProvider()));

            builder.RegisterMessageBroker<MessagePipeSmokeEvent>(options);
            builder.RegisterMessageBroker<GameplaySceneButtonsInteractableChanged>(options);
            builder.RegisterMessageBroker<GameplayGenreBookCountsChanged>(options);
            builder.RegisterMessageBroker<GameplayGenreBookCountsRequested>(options);
            builder.RegisterMessageBroker<GameplaySalesGoldChanged>(options);
            builder.RegisterMessageBroker<SalesCustomerPhaseChanged>(options);
            builder.RegisterMessageBroker<SalesPassiveSaleHappened>(options);
            builder.RegisterMessageBroker<SalesPassivePurchaseFailed>(options);
            builder.RegisterMessageBroker<ResourceCounterCountUpRequested>(options);
            builder.RegisterMessageBroker<GameplayHubReady>(options);
            builder.RegisterMessageBroker<TutorialSequenceStarted>(options);
            builder.RegisterMessageBroker<TutorialStepChanged>(options);
            builder.RegisterMessageBroker<TutorialSequenceCompleted>(options);

            return options;
        }
    }
}
