using Game.Quest.API;
using Game.Quest.Services;
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
            // QuestsService self-registers as ISaveHook in its constructor; AfterLoadAsync builds the
            // catalog from quests.json (configs are warm by then) and activates chain heads.
            builder.Register<QuestsService>(Lifetime.Singleton)
                .As<IQuestsService>();
        }
    }
}
