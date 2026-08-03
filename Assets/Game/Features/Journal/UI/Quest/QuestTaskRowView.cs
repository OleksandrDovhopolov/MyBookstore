using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Quest.UI
{
    public sealed class QuestTaskRowView : MonoBehaviour, ICleanup
    {
        [SerializeField] private TextMeshProUGUI _descriptionLabel;
        [SerializeField] private Slider _progressSlider;
        [SerializeField] private TextMeshProUGUI _counterLabel;
        [SerializeField] private GameObject _doneBadge;

        public void Bind(QuestTaskItemModel model)
        {
            if (_descriptionLabel != null) _descriptionLabel.text = model?.DescriptionKey ?? string.Empty;
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
            if (_progressSlider == null) return;

            _progressSlider.minValue = 0f;
            _progressSlider.maxValue = 100f;
            _progressSlider.SetValueWithoutNotify(Mathf.Clamp01(fill01) * 100f);
        }
    }
}
