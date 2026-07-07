using Game.UI;
using UnityEngine;

namespace Game.UI.ContentWidget
{
    public sealed class ContentWidgetArgs : WindowArgs
    {
        public ContentWidgetDataBase Data { get; }
        public RectTransform Anchor { get; }

        public ContentWidgetArgs(
            ContentWidgetDataBase data,
            RectTransform anchor,
            IWindowController parent = null)
        {
            Data = data;
            Anchor = anchor;

            AsAdditional();
            if (parent != null)
                WithParent(parent);
        }
    }
}
