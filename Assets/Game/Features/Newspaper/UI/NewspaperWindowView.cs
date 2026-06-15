using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Newspaper.UI
{
    public sealed class NewspaperWindowView : WindowView
    {
        [Header("Free decor offer")]
        [SerializeField] private GameObject _freeDecorPanel;
        [SerializeField] private TMP_Text _freeDecorLabel;
        [SerializeField] private Button _freeDecorClaimButton;

        [Header("Paid decor offer")]
        [SerializeField] private GameObject _paidDecorPanel;
        [SerializeField] private TMP_Text _paidDecorLabel;
        [SerializeField] private Button _paidDecorBuyButton;

        [Header("Close")]
        [SerializeField] private Button _closeButton;

        public GameObject FreeDecorPanel => _freeDecorPanel;
        public TMP_Text FreeDecorLabel => _freeDecorLabel;
        public Button FreeDecorClaimButton => _freeDecorClaimButton;

        public GameObject PaidDecorPanel => _paidDecorPanel;
        public TMP_Text PaidDecorLabel => _paidDecorLabel;
        public Button PaidDecorBuyButton => _paidDecorBuyButton;

        public Button CloseButton => _closeButton;
    }
}
