using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Book.Sell.UI.Customer
{
    // Holds references to the bubble content the controller actually drives: the text indicator
    // (animated dots / labels) and the passive-sale result icons.
    public sealed class CustomerThoughtBubbleView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _stateText;
        [SerializeField] private TextMeshProUGUI _dotsText;
        [SerializeField] private Image _successIcon;
        [SerializeField] private Image _failIcon;

        public TextMeshProUGUI StateText => _stateText;
        public TextMeshProUGUI DotsText => _dotsText;
        public Image SuccessIcon => _successIcon;
        public Image FailIcon => _failIcon;
    }
}
