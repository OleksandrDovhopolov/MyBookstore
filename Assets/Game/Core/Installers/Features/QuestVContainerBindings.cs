using VContainer;

namespace Game.Bootstrap
{
    // Registered in: GameInstaller (GameplayLifetimeScope)
    // Resolves from parent: ISaveService, IAnalyticsService
    public static class QuestVContainerBindings
    {
        public static void RegisterQuest(this IContainerBuilder builder)
        {
            // TODO: Quest config (quest definitions — ScriptableObject)
            // builder.RegisterInstance(_questConfig);

            // TODO: Quest progress storage
            // builder.Register<IQuestProgressStorage, SaveBackedQuestProgressStorage>(Lifetime.Singleton);

            // TODO: Quest condition evaluators (each quest type gets its own evaluator)
            // builder.Register<IQuestConditionEvaluator, BookPurchaseConditionEvaluator>(Lifetime.Singleton);

            // TODO: Quest service — tracks active quests, evaluates completion
            // builder.Register<IQuestService, QuestService>(Lifetime.Singleton);

            // TODO: Quest reward grant (delegates to IRewardGrantService)
            // builder.Register<IQuestRewardApplier, QuestRewardApplier>(Lifetime.Singleton);

            // TODO: Quest UI entry point / facade (if using EntryPoints pattern)
            // builder.RegisterEntryPoint<QuestSystemFacade>().As<IQuestSystemFacade>();
        }
    }
}
