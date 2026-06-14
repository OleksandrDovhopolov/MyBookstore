using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Common
{
    public sealed class SettingsWindowView : WindowView
    {
        [SerializeField] private TextMeshProUGUI _openCounterLabel;
        [SerializeField] private TextMeshProUGUI _confirmResultLabel;
        [SerializeField] private Button _resetButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _openConfirmButton;

        public TextMeshProUGUI OpenCounterLabel => _openCounterLabel;
        public TextMeshProUGUI ConfirmResultLabel => _confirmResultLabel;
        public Button ResetButton => _resetButton;
        public Button CloseButton => _closeButton;
        public Button OpenConfirmButton => _openConfirmButton;
    }
}
