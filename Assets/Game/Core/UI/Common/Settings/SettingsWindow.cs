using Cysharp.Threading.Tasks;

namespace Game.UI.Common
{
    [Window("SettingsWindow", WindowType.Page, keepInCache: true)]
    public sealed class SettingsWindow : WindowController<SettingsWindowView>
    {
        private int _openCount;

        protected override void OnInit()
        {
            View.ResetButton.onClick.AddListener(OnResetClicked);
            View.CloseButton.onClick.AddListener(OnCloseClicked);
            View.OpenConfirmButton.onClick.AddListener(OnOpenConfirmClicked);
            View.ConfirmResultLabel.text = string.Empty;
        }

        protected override void OnShowStart()
        {
            _openCount++;
            UpdateOpenCountLabel();
        }

        protected override void OnDispose()
        {
            if (View == null) return;
            View.ResetButton.onClick.RemoveListener(OnResetClicked);
            View.CloseButton.onClick.RemoveListener(OnCloseClicked);
            View.OpenConfirmButton.onClick.RemoveListener(OnOpenConfirmClicked);
        }

        private void OnResetClicked()
        {
            _openCount = 0;
            UpdateOpenCountLabel();
        }

        private void OnCloseClicked() => CloseAsync().Forget();

        private void OnOpenConfirmClicked() => ShowConfirmAsync().Forget();

        private async UniTask ShowConfirmAsync()
        {
            var args = new ConfirmDialogArgs(
                title: "Demo",
                body: "Click Confirm or Cancel — the result is shown back here.",
                confirmLabel: "Confirm",
                cancelLabel: "Cancel").WithParent(this);

            var dialog = await UIManager.ShowAsync<ConfirmDialog>((WindowArgs)args);
            if (dialog == null) return;

            var result = await dialog.WaitForResultAsync<ConfirmDialogResult>();
            View.ConfirmResultLabel.text = $"Result: {result}";
        }

        private void UpdateOpenCountLabel()
        {
            View.OpenCounterLabel.text = $"Open count: {_openCount}";
        }
    }
}
