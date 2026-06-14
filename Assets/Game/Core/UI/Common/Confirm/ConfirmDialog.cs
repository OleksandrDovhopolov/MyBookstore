using Cysharp.Threading.Tasks;

namespace Game.UI.Common
{
    [Window("UI/Common/ConfirmDialog", WindowType.Popup)]
    public sealed class ConfirmDialog : WindowController<ConfirmDialogView>, IResultWindow<ConfirmDialogResult>
    {
        public ConfirmDialogResult Result { get; private set; } = ConfirmDialogResult.None;

        protected override void OnInit()
        {
            View.ConfirmButton.onClick.AddListener(OnConfirmClicked);
            View.CancelButton.onClick.AddListener(OnCancelClicked);
        }

        protected override void OnShowStart()
        {
            ApplyArgsToView();
            Result = ConfirmDialogResult.None;
        }

        protected override void UpdateWindow() => ApplyArgsToView();

        protected override void OnDispose()
        {
            if (View != null)
            {
                View.ConfirmButton.onClick.RemoveListener(OnConfirmClicked);
                View.CancelButton.onClick.RemoveListener(OnCancelClicked);
            }
        }

        private void ApplyArgsToView()
        {
            if (Arguments is not ConfirmDialogArgs args) return;
            View.TitleLabel.text = args.Title;
            View.BodyLabel.text = args.Body;
            View.ConfirmButtonLabel.text = args.ConfirmLabel;
            View.CancelButtonLabel.text = args.CancelLabel;
        }

        private void OnConfirmClicked()
        {
            Result = ConfirmDialogResult.Confirmed;
            CloseAsync().Forget();
        }

        private void OnCancelClicked()
        {
            Result = ConfirmDialogResult.Cancelled;
            CloseAsync().Forget();
        }
    }
}
