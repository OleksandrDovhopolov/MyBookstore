using Game.Preparation.Services;
using VContainer;

namespace Game.Bootstrap
{
    // Preparation phase. Регистрируется ГЛОБАЛЬНО (BootstrapInstaller), т.к. UI стал окном
    // PreparationWindow, а окна инжектятся глобальным resolver-ом (AddressablesWindowFactory).
    // Обе зависимости резолвятся из Global: ISaveService, IDayProgressService, IConfigsService,
    // IInventoryService, ISalesShelfStateService.
    //
    // ISalesSetupProvider (PreparationSalesSetupProvider) регистрируется отдельно в LocationInstaller
    // (location-скоп), т.к. его потребитель — SalesDayController (BookSell) в LocationScene. Provider
    // читает выбор игрока из save-модуля preparation.session. См. docs/GameFlowLoop.md.
    public static class PreparationVContainerBindings
    {
        public static void RegisterPreparation(this IContainerBuilder builder)
        {
            builder.Register<IPreparationInventoryProvider, DayProgressInventoryProvider>(Lifetime.Singleton);
            builder.Register<IPreparationSessionService, PreparationSessionService>(Lifetime.Singleton);
        }
    }
}
