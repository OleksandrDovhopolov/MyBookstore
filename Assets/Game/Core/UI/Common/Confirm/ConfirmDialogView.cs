using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Common
{
    public sealed class ConfirmDialogView : WindowView
    {
        [SerializeField] private TextMeshProUGUI _titleLabel;
        [SerializeField] private TextMeshProUGUI _bodyLabel;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private TextMeshProUGUI _confirmButtonLabel;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private TextMeshProUGUI _cancelButtonLabel;

        public TextMeshProUGUI TitleLabel => _titleLabel;
        public TextMeshProUGUI BodyLabel => _bodyLabel;
        public Button ConfirmButton => _confirmButton;
        public TextMeshProUGUI ConfirmButtonLabel => _confirmButtonLabel;
        public Button CancelButton => _cancelButton;
        public TextMeshProUGUI CancelButtonLabel => _cancelButtonLabel;
    }
}
