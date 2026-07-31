using Cysharp.Threading.Tasks;
using Game.UI;

namespace Game.UI.ContentWidget
{
    [Window("ContentWidget", WindowType.Widget, keepInCache: true)]
    public sealed class ContentWidgetController : WindowController<ContentWidgetView>
    {
        protected override void OnInit()
        {
            UIManager.WindowShown += OnWindowShown;
        }

        protected override void OnShowStart()
        {
            if (Arguments is ContentWidgetArgs args)
            {
                View.ShowContentView(args.Data, args.Anchor, args.AutoCloseEnabled, args.PlacementMode);
                return;
            }

            CloseAsync().Forget();
        }

        protected override void OnHideStart(bool isClosed)
        {
            View.HideContent();
        }

        protected override void OnDispose()
        {
            if (UIManager != null)
                UIManager.WindowShown -= OnWindowShown;
        }

        // Dismiss when another (non-widget) window opens on top: the widget's context has changed.
        // Parent-close is already handled by the UIManager child cascade, so this only covers new,
        // unrelated opens. Must stay fire-and-forget — the event fires under the manager's show gate.
        private void OnWindowShown(IWindowController controller)
        {
            // The controller is keepInCache, so it stays subscribed while hidden.
            if (!IsShown) return;
            if (controller == null) return;
            if (ReferenceEquals(controller, this)) return;

            var type = controller.Attribute.Type;
            if (type == WindowType.Widget || type == WindowType.HUD) return;

            CloseAsync().Forget();
        }
    }
}
