using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Book.Sell.UI
{
    /// <summary>
    /// View for the active book-recommendation minigame window. Holds the selection UI
    /// (<see cref="MinigameRoot"/>: request line, shelf grid, book-detail panel, Recommend/Skip) and the
    /// post-recommendation result container (<see cref="ResultPanel"/>: customer emotion + Finish).
    /// All logic lives in <see cref="RecommendationMinigameWindow"/>; this only exposes serialized refs.
    /// </summary>
    public sealed class RecommendationMinigameWindowView : WindowView
    {
        [Header("Minigame (selection) root")]
        [SerializeField] private GameObject _minigameRoot;

        [Header("Active request")]
        [SerializeField] private TMP_Text _requestText;
        [SerializeField] private TMP_Text _difficultyLabel;     // "Difficulty: 3/5" — empty when none/Unknown

        [Header("Shelf (grid)")]
        [SerializeField] private Transform _shelfContainer;
        [SerializeField] private BookCardView _bookCardPrefab;

        [Header("Book detail panel")]
        [SerializeField] private GameObject _detailPanel;
        [SerializeField] private TMP_Text _detailTitle;
        [SerializeField] private TMP_Text _detailAuthor;
        [SerializeField] private TMP_Text _detailDescription;
        [SerializeField] private TMP_Text _detailPublishDate;
        [SerializeField] private TMP_Text _detailPageCount;

        [Header("Actions")]
        [SerializeField] private Button _recommendButton;       // Confirm the selected book
        [SerializeField] private Button _skipButton;            // "I couldn't find the right book"

        [Header("Result container")]
        [SerializeField] private GameObject _resultPanel;
        [SerializeField] private TMP_Text _emotionLabel;        // customer reaction (mapped from Tier)
        [SerializeField] private Button _finishButton;

        public GameObject MinigameRoot => _minigameRoot;

        public TMP_Text RequestText => _requestText;
        public TMP_Text DifficultyLabel => _difficultyLabel;

        public Transform ShelfContainer => _shelfContainer;
        public BookCardView BookCardPrefab => _bookCardPrefab;

        public GameObject DetailPanel => _detailPanel;
        public TMP_Text DetailTitle => _detailTitle;
        public TMP_Text DetailAuthor => _detailAuthor;
        public TMP_Text DetailDescription => _detailDescription;
        public TMP_Text DetailPublishDate => _detailPublishDate;
        public TMP_Text DetailPageCount => _detailPageCount;

        public Button RecommendButton => _recommendButton;
        public Button SkipButton => _skipButton;

        public GameObject ResultPanel => _resultPanel;
        public TMP_Text EmotionLabel => _emotionLabel;
        public Button FinishButton => _finishButton;
    }
}
