using Game.Shop.UI.Sections;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Shop.UI
{
    public sealed class ClassicShopWindowView : WindowView
    {
        [Header("Tabs")]
        [SerializeField] private Button _booksTabButton;
        [SerializeField] private Button _boxesTabButton;
        [SerializeField] private Button _decorTabButton;
        [SerializeField] private TMP_Text _activeTabLabel;

        [Header("Sections (one bound per tab)")]
        [SerializeField] private ShopLotsSectionView _booksSection;
        [SerializeField] private ShopLotsSectionView _boxesSection;
        [SerializeField] private ShopLotsSectionView _decorSection;

        [Header("Close")]
        [SerializeField] private Button _closeButton;

        public Button BooksTabButton => _booksTabButton;
        public Button BoxesTabButton => _boxesTabButton;
        public Button DecorTabButton => _decorTabButton;
        public TMP_Text ActiveTabLabel => _activeTabLabel;

        public ShopLotsSectionView BooksSection => _booksSection;
        public ShopLotsSectionView BoxesSection => _boxesSection;
        public ShopLotsSectionView DecorSection => _decorSection;

        public Button CloseButton => _closeButton;
    }
}
