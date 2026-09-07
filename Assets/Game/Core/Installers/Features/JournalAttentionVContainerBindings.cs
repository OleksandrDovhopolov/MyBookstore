using Game.Journal.UI;
using VContainer;

namespace Game.Bootstrap
{
    public static class JournalAttentionVContainerBindings
    {
        public static void RegisterJournalAttention(this IContainerBuilder builder)
        {
            builder.Register<IJournalAttentionRepository, SaveBackedJournalAttentionRepository>(Lifetime.Singleton);
            builder.Register<JournalAttentionService>(Lifetime.Singleton)
                .As<IJournalAttentionService>();
        }
    }
}
