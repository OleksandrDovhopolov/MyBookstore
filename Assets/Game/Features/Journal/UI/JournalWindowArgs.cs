using Game.UI;

namespace Game.Journal.UI
{
    public sealed class JournalWindowArgs : WindowArgs
    {
        public JournalWindowArgs(JournalTab? tab = null)
        {
            Tab = tab;
            AsMain();
        }

        public JournalTab? Tab { get; }
    }
}
