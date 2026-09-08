using Game.UI;
using Game.UI.ContentWidget;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Book.Sell.UI
{
    /// <summary>
    /// View for the active book-recommendation minigame window. Holds the selection UI
    /// (<see cref="MinigameRoot"/>: request line, shelf grid, book-detail area, Recommend/Skip) and the
    /// post-recommendation result controls (<see cref="ResultPanel"/>: customer emotion + Finish).
    ///
    /// The detail area is on screen the whole time and swaps between two containers:
    /// <see cref="DetailEmptyRoot"/> (nothing picked — authored placeholder text only) and
    /// <see cref="DetailSelectedRoot"/> (the picked book's title/author/description/date/pages).
    ///
    /// All logic lives in <see cref="RecommendationMinigameWindow"/>; this only exposes serialized refs.
    /// </summary>
    public sealed class RecommendationMinigameWindowView : WindowView
    {
        [Header("Minigame (selection) root")]
        [SerializeField] private GameObject _minigameRoot;
        [SerializeField] private RecommendationMinigameAnimator _animator;

        [Header("Active request")]
        [SerializeField] private TMP_Text _requestText;

        [Header("Shelf (grid)")]
        [SerializeField] private Transform _shelfContainer;
        [SerializeField] private BookCardView _bookCardPrefab;
        [SerializeField] private BookInfoWidgetView _bookInfoWidgetPrefab;
        [SerializeField] private Button _clearFocusButton;      // Transparent/background button for clearing book focus

        [Header("Book detail — no book selected")]
        [SerializeField] private GameObject _detailEmptyRoot;

        [Header("Book detail — book selected")]
        [SerializeField] private GameObject _detailSelectedRoot;
        [SerializeField] private TMP_Text _detailTitle;
        [SerializeField] private TMP_Text _detailAuthor;
        [SerializeField] private TMP_Text _detailDescription;
        [SerializeField] private TMP_Text _detailPublishDate;
        [SerializeField] private TMP_Text _detailPageCount;

        [Header("Actions")]
        [SerializeField] private Button _recommendButton;       // Confirm the selected book
        [SerializeField] private Button _skipButton;            // "I couldn't find the right book"

        [Header("Result container")]
        [SerializeField] private GameObject _successResultObject;
        [SerializeField] private GameObject _failResultObject;
        [SerializeField] private TMP_Text _emotionLabel;        // customer reaction (mapped from Tier)
        [SerializeField] private Button _finishButton;

        public GameObject MinigameRoot => _minigameRoot;
        public RecommendationMinigameAnimator Animator => _animator;

        public TMP_Text RequestText => _requestText;

        public Transform ShelfContainer => _shelfContainer;
        public BookCardView BookCardPrefab => _bookCardPrefab;
        public Button ClearFocusButton => _clearFocusButton;

        public GameObject DetailEmptyRoot => _detailEmptyRoot;
        public GameObject DetailSelectedRoot => _detailSelectedRoot;
        public TMP_Text DetailTitle => _detailTitle;
        public TMP_Text DetailAuthor => _detailAuthor;
        public TMP_Text DetailDescription => _detailDescription;
        public TMP_Text DetailPublishDate => _detailPublishDate;
        public TMP_Text DetailPageCount => _detailPageCount;

        public Button RecommendButton => _recommendButton;
        public Button SkipButton => _skipButton;

        //public GameObject ResultPanel => _resultPanel;
        public GameObject SuccessResultObject => _successResultObject;
        public GameObject FailResultObject => _failResultObject;
        public TMP_Text EmotionLabel => _emotionLabel;
        public Button FinishButton => _finishButton;

        protected override void Awake()
        {
            base.Awake();
            if (_bookInfoWidgetPrefab != null)
                WidgetRegistry.Register<BookInfoWidgetData>(_bookInfoWidgetPrefab);
        }
    }
}
