using Cysharp.Threading.Tasks;
using Game.UI;

namespace Game.Shop
{
    public class InfoWidgetArg : WindowArgs
    {
        public string Text;
    }
    
    [Window("InfoWidget", WindowType.Widget, keepInCache:true)]
    public class InfoWidgetController : WindowController<InfoWidgetView>
    {
        protected override void OnShowStart()
        {
            ShowHint();
        }

        protected override void OnShowComplete()
        {
            View.OnComplete += CloseWidget;
        }
        
        private void ShowHint()
        {
            View.ShowHint(((InfoWidgetArg) Arguments).Text);
        }

        protected override void OnHideStart(bool isClosed)
        {
            View.OnComplete -= CloseWidget;
        }

        private void CloseWidget()
        {
            UIManager.HideAsync<InfoWidgetController>().Forget();
        }
    }
}
