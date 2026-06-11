using Book.Sell.Services;
using Book.Sell.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    // Main Feature (core loop) — фаза «Продажа».
    // Registered in: GameInstaller (GameplayLifetimeScope).
    // Resolves from parent (GlobalLifetimeScope): IConfigsService.
    // Без ISaveService / IDayProgressService — Sales standalone в текущей итерации.
    public static class BookSellVContainerBindings
    {
        public static void RegisterBookSell(this IContainerBuilder builder)
        {
            builder.Register<ISalesRandom, UnityRandomSalesRandom>(Lifetime.Singleton);
            builder.Register<ISalesSetupProvider, DefaultSalesSetupProvider>(Lifetime.Singleton);
            builder.Register<IRecommendationScoringService, RecommendationScoringService>(Lifetime.Singleton);
            builder.Register<IPassiveSaleSelector, DefaultPassiveSaleSelector>(Lifetime.Singleton);
            builder.Register<ISalesSessionService, SalesSessionService>(Lifetime.Singleton);

            // Debug-экран. Регистрируем, только если он есть в сцене, чтобы проект собирался
            // и до ручной расстановки UI. То же поведение, что у MorningScreenView.
            if (Object.FindAnyObjectByType<SalesScreenView>(FindObjectsInactive.Include) != null)
                builder.RegisterComponentInHierarchy<SalesScreenView>();
        }
    }
}
