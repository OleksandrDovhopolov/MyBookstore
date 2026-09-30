using Game.Attention;
using Game.Attention.API;
using Game.Journal.UI;
using VContainer;

namespace Game.Bootstrap
{
    /// <summary>
    /// Registers the shared "there is something new here" state: one <see cref="AttentionService"/>
    /// plus every feature's source. Replaces the old Journal-only attention service — the Journal and
    /// the Shop now badge off the same store (save module <c>attention</c>).
    /// </summary>
    public static class AttentionVContainerBindings
    {
        public static void RegisterAttention(this IContainerBuilder builder)
        {
            builder.Register<IAttentionRepository, SaveBackedAttentionRepository>(Lifetime.Singleton);

            // Journal sources. Quests is two keys, not one: "became active" and "ready to award" keep
            // separate seen-sets.
            builder.Register<IAttentionSource, QuestNewAttentionSource>(Lifetime.Singleton);
            builder.Register<IAttentionSource, QuestAwardAttentionSource>(Lifetime.Singleton);
            builder.Register<IAttentionSource, PlaceAttentionSource>(Lifetime.Singleton);
            builder.Register<IAttentionSource, PeopleAttentionSource>(Lifetime.Singleton);

            // Shop source — cross-feature glue, see ShopDecorAttentionSource for why it lives here.
            builder.Register<IAttentionSource, ShopDecorAttentionSource>(Lifetime.Singleton);

            // Memories persist their seen-state in the "characters" module, so they come in as a flag
            // source that the attention service never writes.
            builder.Register<IAttentionFlagSource, MemoriesAttentionFlagSource>(Lifetime.Singleton);

            builder.Register<AttentionService>(Lifetime.Singleton)
                .As<IAttentionService>();
        }
    }
}
