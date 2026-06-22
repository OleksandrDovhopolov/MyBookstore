using Game.Newspaper.UI;
using VContainer;

namespace Game.Bootstrap
{
    public static class NewspaperVContainerBindings
    {
        public static void RegisterNewspaper(this IContainerBuilder builder)
        {
            builder.Register<INewspaperOfferSource, ShopBackedNewspaperOfferSource>(Lifetime.Singleton);
        }
    }
}
