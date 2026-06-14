namespace Game.UI.Common
{
    public sealed class ConfirmDialogArgs : WindowArgs
    {
        public string Title { get; }
        public string Body { get; }
        public string ConfirmLabel { get; }
        public string CancelLabel { get; }

        public ConfirmDialogArgs(
            string title,
            string body,
            string confirmLabel = "OK",
            string cancelLabel = "Cancel")
        {
            Title = title;
            Body = body;
            ConfirmLabel = confirmLabel;
            CancelLabel = cancelLabel;
            AsAdditional();
        }
    }
}
