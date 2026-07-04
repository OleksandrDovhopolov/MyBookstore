using TMPro;
using UIShared;
using UnityEngine;

namespace Game.Quest.UI
{
    /// <summary>
    /// One quest row: title, description, primary-task progress ("X/Y") and a state label, plus an optional
    /// "complete" badge. Text fields are raw localization keys until INF-4. Pooled via <c>UIListPool</c>.
    /// </summary>
    public sealed class QuestRowView : MonoBehaviour, ICleanup
    {
        [SerializeField] private TextMeshProUGUI _titleLabel;
        [SerializeField] private TextMeshProUGUI _descriptionLabel;
        [SerializeField] private TextMeshProUGUI _progressLabel;
        [SerializeField] private TextMeshProUGUI _stateLabel;
        [SerializeField] private GameObject _completeBadge; // optional checkmark for completed quests

        public void Bind(QuestItemModel model)
        {
            if (_titleLabel != null) _titleLabel.text = model.TitleKey ?? string.Empty;
            if (_descriptionLabel != null) _descriptionLabel.text = model.DescriptionKey ?? string.Empty;
            if (_progressLabel != null) _progressLabel.text = $"{model.ProgressCurrent}/{model.ProgressGoal}";
            if (_stateLabel != null) _stateLabel.text = model.State.ToString();
            if (_completeBadge != null) _completeBadge.SetActive(model.IsComplete);
        }

        // Called by UIListPool when the row is (re)acquired or disabled so a pooled instance never shows
        // stale data.
        public void Cleanup()
        {
            if (_titleLabel != null) _titleLabel.text = string.Empty;
            if (_descriptionLabel != null) _descriptionLabel.text = string.Empty;
            if (_progressLabel != null) _progressLabel.text = string.Empty;
            if (_stateLabel != null) _stateLabel.text = string.Empty;
            if (_completeBadge != null) _completeBadge.SetActive(false);
        }
    }
}
