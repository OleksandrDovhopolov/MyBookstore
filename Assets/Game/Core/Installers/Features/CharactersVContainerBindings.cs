using Game.Characters.API;
using Game.Characters.Services;
using Game.Characters.Services.Persistence;
using VContainer;
// Note: ICharacterModelFactory is internal to Game.Characters and built inside CharactersService —
// it is intentionally not registered here.

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope) after RegisterQuest — CharactersService is an
    // ISaveHook (built before SaveDataLoadOperation) and reads IQuestsService to derive memory/discovery
    // state. Resolves from the same scope: ISaveService, IConfigsService, IQuestsService.
    public static class CharactersVContainerBindings
    {
        public static void RegisterCharacters(this IContainerBuilder builder)
        {
            // Local save-module-backed persistence (Stage 1: discovered flag only).
            builder.Register<ICharactersRepository, SaveBackedCharactersRepository>(Lifetime.Singleton);

            // CharactersService self-registers as ISaveHook in its constructor; AfterLoadAsync builds the
            // catalog from characters.json (configs are warm by then) and loads saved state.
            builder.Register<CharactersService>(Lifetime.Singleton)
                .As<ICharactersService>();
        }
    }
}
