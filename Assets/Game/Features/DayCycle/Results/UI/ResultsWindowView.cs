using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.DayCycle.Results.UI
{
    public sealed class ResultsWindowView : WindowView
    {
        [Header("Header")]
        [SerializeField] private TMP_Text _dayLabel;
        [SerializeField] private TMP_Text _salesLabel;
        [SerializeField] private TMP_Text _goldLabel;

        [Header("Tier counts")]
        [SerializeField] private TMP_Text _excLabel;
        [SerializeField] private TMP_Text _normLabel;
        [SerializeField] private TMP_Text _failLabel;
        [SerializeField] private TMP_Text _skipLabel;

        [Header("Best match")]
        [SerializeField] private GameObject _bestMatchPanel;
        [SerializeField] private TMP_Text _bestBookLabel;
        [SerializeField] private TMP_Text _bestRequestLabel;
        [SerializeField] private TMP_Text _bestTierLabel;
        [SerializeField] private TMP_Text _bestReasonLabel;

        [Header("Review")]
        [SerializeField] private TMP_Text _reviewLabel;

        [Header("Reward line")]
        [SerializeField] private TMP_Text _goldDeltaLabel;
        [SerializeField] private TMP_Text _repDeltaLabel;
        [SerializeField] private TMP_Text _alreadyAppliedHint;

        [Header("Actions")]
        [SerializeField] private Button _nextDayButton;
        [SerializeField] private Button _openNewspaperButton;

        [Header("Error")]
        [SerializeField] private GameObject _errorPanel;

        [Header("Close")]
        [SerializeField] private Button _closeButton;

        public TMP_Text DayLabel => _dayLabel;
        public TMP_Text SalesLabel => _salesLabel;
        public TMP_Text GoldLabel => _goldLabel;
        public TMP_Text ExcellentLabel => _excLabel;
        public TMP_Text NormalLabel => _normLabel;
        public TMP_Text FailedLabel => _failLabel;
        public TMP_Text SkippedLabel => _skipLabel;

        public GameObject BestMatchPanel => _bestMatchPanel;
        public TMP_Text BestBookLabel => _bestBookLabel;
        public TMP_Text BestRequestLabel => _bestRequestLabel;
        public TMP_Text BestTierLabel => _bestTierLabel;
        public TMP_Text BestReasonLabel => _bestReasonLabel;

        public TMP_Text ReviewLabel => _reviewLabel;
        public TMP_Text GoldDeltaLabel => _goldDeltaLabel;
        public TMP_Text ReputationDeltaLabel => _repDeltaLabel;
        public TMP_Text AlreadyAppliedHint => _alreadyAppliedHint;

        public Button NextDayButton => _nextDayButton;
        public Button OpenNewspaperButton => _openNewspaperButton;
        public GameObject ErrorPanel => _errorPanel;
        public Button CloseButton => _closeButton;
    }
}
