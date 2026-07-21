using Game.UI;
using UnityEngine;

namespace Game.UI.ContentWidget
{
    public sealed class ContentWidgetArgs : WindowArgs
    {
        public ContentWidgetDataBase Data { get; }
        public RectTransform Anchor { get; }
        public bool AutoCloseEnabled { get; }

        public ContentWidgetArgs(
            ContentWidgetDataBase data,
            RectTransform anchor,
            IWindowController parent = null,
            bool autoCloseEnabled = true)
        {
            Data = data;
            Anchor = anchor;
            AutoCloseEnabled = autoCloseEnabled;

            AsAdditional();
            if (parent != null)
                WithParent(parent);
        }
    }
}
