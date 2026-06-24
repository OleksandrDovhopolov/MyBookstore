using Game.Newspaper.UI;
using VContainer;

namespace Game.Bootstrap
{
    public static class UiSpritesVContainerBindings
    {
        public static void RegisterUiSprites(this IContainerBuilder builder, UiSpriteCatalog catalog)
        {
            builder.RegisterInstance(catalog);
            builder.Register<IUiSpriteProvider, UiSpriteProvider>(Lifetime.Singleton);
        }
    }
}
