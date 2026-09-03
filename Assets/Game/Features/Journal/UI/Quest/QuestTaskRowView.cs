using Game.Localization;
using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Quest.UI
{
    public sealed class QuestTaskRowView : MonoBehaviour, ICleanup
    {
        [SerializeField] private TextMeshProUGUI _descriptionLabel;

        [Tooltip("Image with Type = Filled; fillAmount is driven by task progress in the 0..1 range.")]
        [SerializeField] private Image _progressFill;

        [SerializeField] private TextMeshProUGUI _counterLabel;
        [SerializeField] private GameObject _doneBadge;

        public void Bind(QuestTaskItemModel model)
        {
            if (_descriptionLabel != null) _descriptionLabel.text = LocalizationLocator.GetOrKey(model?.DescriptionKey);
            ApplyProgress(model != null ? model.Fill01 : 0f);
            if (_counterLabel != null)
                _counterLabel.text = model != null ? $"{model.Current}/{model.Goal}" : string.Empty;
            if (_doneBadge != null) _doneBadge.SetActive(model?.IsDone == true);
        }

        public void Cleanup()
        {
            if (_descriptionLabel != null) _descriptionLabel.text = string.Empty;
            ApplyProgress(0f);
            if (_counterLabel != null) _counterLabel.text = string.Empty;
            if (_doneBadge != null) _doneBadge.SetActive(false);
        }

        private void ApplyProgress(float fill01)
        {
            if (_progressFill == null) return;

            _progressFill.fillAmount = Mathf.Clamp01(fill01);
        }
    }
}
