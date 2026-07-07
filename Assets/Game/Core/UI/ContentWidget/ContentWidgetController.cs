using Cysharp.Threading.Tasks;
using Game.UI;

namespace Game.UI.ContentWidget
{
    [Window("ContentWidget", WindowType.Widget, keepInCache: true)]
    public sealed class ContentWidgetController : WindowController<ContentWidgetView>
    {
        protected override void OnShowStart()
        {
            if (Arguments is ContentWidgetArgs args)
            {
                View.ShowContentView(args.Data, args.Anchor);
                return;
            }

            CloseAsync().Forget();
        }

        protected override void OnHideStart(bool isClosed)
        {
            View.HideContent();
        }
    }
}
