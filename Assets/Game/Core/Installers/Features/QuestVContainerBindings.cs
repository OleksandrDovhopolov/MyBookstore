using Game.Quest.API;
using Game.Quest.Services;
using Game.Quest.Services.Persistence;
using VContainer;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope) — must be global so its ISaveHook is
    // registered before SaveDataLoadOperation (Bootstrap.Construct force-constructs it). Resolves from
    // the same scope: ISaveService, IConfigsService, IConditionParser, and (optional) ISalesStatsService,
    // IDecorPlacementService, IInventoryService, IDayProgressService — the data sources it re-evaluates on.
    public static class QuestVContainerBindings
    {
        public static void RegisterQuest(this IContainerBuilder builder)
        {
            // Local save-module-backed persistence (Awarded/Failed terminals + Active/RTA task progress).
            builder.Register<IQuestsRepository, SaveBackedQuestsRepository>(Lifetime.Singleton);

            // QuestsService self-registers as ISaveHook in its constructor; AfterLoadAsync builds the
            // catalog from quests.json (configs are warm by then), restores saved state, activates heads.
            builder.Register<QuestsService>(Lifetime.Singleton)
                .As<IQuestsService>();
        }
    }
}
