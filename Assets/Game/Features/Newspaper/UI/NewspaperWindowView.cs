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

        [Header("Book crates")]
        [SerializeField] private GameObject _bookCratesPanel;
        [SerializeField] private Button _commonBoxBuyButton;
        [SerializeField] private TMP_Text _commonBoxLabel;
        [SerializeField] private Button _rareBoxBuyButton;
        [SerializeField] private TMP_Text _rareBoxLabel;
        [SerializeField] private Button _dystopicBoxBuyButton;
        [SerializeField] private TMP_Text _dystopicBoxLabel;
        [SerializeField] private TMP_Text _lastBookRewardLabel;

        [Header("Close")]
        [SerializeField] private Button _closeButton;

        public GameObject FreeDecorPanel => _freeDecorPanel;
        public TMP_Text FreeDecorLabel => _freeDecorLabel;
        public Button FreeDecorClaimButton => _freeDecorClaimButton;

        public GameObject PaidDecorPanel => _paidDecorPanel;
        public TMP_Text PaidDecorLabel => _paidDecorLabel;
        public Button PaidDecorBuyButton => _paidDecorBuyButton;

        public GameObject BookCratesPanel => _bookCratesPanel;
        public Button CommonBoxBuyButton => _commonBoxBuyButton;
        public TMP_Text CommonBoxLabel => _commonBoxLabel;
        public Button RareBoxBuyButton => _rareBoxBuyButton;
        public TMP_Text RareBoxLabel => _rareBoxLabel;
        public Button DystopicBoxBuyButton => _dystopicBoxBuyButton;
        public TMP_Text DystopicBoxLabel => _dystopicBoxLabel;
        public TMP_Text LastBookRewardLabel => _lastBookRewardLabel;

        public Button CloseButton => _closeButton;
    }
}
