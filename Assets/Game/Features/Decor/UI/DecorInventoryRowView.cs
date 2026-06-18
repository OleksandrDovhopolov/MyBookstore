using Game.Configs.Models;
using TMPro;
using UnityEngine;

namespace Game.Decor.UI
{
    public sealed class DecorInventoryRowView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _nameLabel;
        [SerializeField] private TextMeshProUGUI _statusLabel;
        [SerializeField] private TextMeshProUGUI _effectsLabel;

        public void Bind(DecorConfig config, string placedSlotId, Color positive, Color negative)
        {
            if (_nameLabel != null)
                _nameLabel.text = $"{config.DisplayName} [{config.PositionType} / {config.Size}]";

            if (_statusLabel != null)
            {
                _statusLabel.text = string.IsNullOrEmpty(placedSlotId)
                    ? "<i>not placed</i>"
                    : $"placed in {placedSlotId}";
            }

            if (_effectsLabel != null)
                _effectsLabel.text = DecorSlotRowView.FormatEffects(config, positive, negative);
        }
    }
}
