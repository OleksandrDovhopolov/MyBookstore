using UIShared;

namespace Game.Journal.UI
{
    /// <summary>
    /// Concrete tab button over <see cref="JournalTab"/>. Serialized field names match the generic
    /// base (<c>_tab</c>, <c>_toggle</c>), so values authored in JournalWindow.prefab survive.
    /// </summary>
    public sealed class JournalTabButton : TabButton<JournalTab>
    {
    }
}
