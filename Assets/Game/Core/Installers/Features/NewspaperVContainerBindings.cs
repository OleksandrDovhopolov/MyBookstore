using Game.Newspaper.UI;
using VContainer;

namespace Game.Bootstrap
{
    public static class NewspaperVContainerBindings
    {
        public static void RegisterNewspaper(this IContainerBuilder builder)
        {
            builder.Register<IShopOfferSource, ShopOfferSource>(Lifetime.Singleton);
        }
    }
}
