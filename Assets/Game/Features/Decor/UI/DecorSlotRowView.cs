using System;
using System.Text;
using Game.Configs.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Decor.UI
{
    public sealed class DecorSlotRowView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _slotLabel;
        [SerializeField] private TextMeshProUGUI _decorLabel;
        [SerializeField] private TextMeshProUGUI _effectsLabel;
        [SerializeField] private Button _placeButton;
        [SerializeField] private Button _unplaceButton;

        public Action OnPlaceRequested;
        public Action OnUnplaceRequested;

        private void Awake()
        {
            if (_placeButton != null) _placeButton.onClick.AddListener(() => OnPlaceRequested?.Invoke());
            if (_unplaceButton != null) _unplaceButton.onClick.AddListener(() => OnUnplaceRequested?.Invoke());
        }

        public void Bind(DecorSlot slot, DecorConfig placed, Color positive, Color negative)
        {
            if (_slotLabel != null)
                _slotLabel.text = $"{slot.Id} [{slot.PositionType} / {slot.MaxSize}]";

            var occupied = placed != null;
            if (_decorLabel != null)
                _decorLabel.text = occupied ? placed.DisplayName : "<i>Empty</i>";

            if (_effectsLabel != null)
                _effectsLabel.text = occupied ? FormatEffects(placed, positive, negative) : string.Empty;

            if (_placeButton != null) _placeButton.gameObject.SetActive(!occupied);
            if (_unplaceButton != null) _unplaceButton.gameObject.SetActive(occupied);
        }

        public static string FormatEffects(DecorConfig config, Color positive, Color negative)
        {
            if (config?.GenreMultipliers == null || config.GenreMultipliers.Length == 0)
                return "(no effects)";

            var sb = new StringBuilder();
            for (var i = 0; i < config.GenreMultipliers.Length; i++)
            {
                var mod = config.GenreMultipliers[i];
                if (mod == null) continue;
                var percent = Mathf.RoundToInt((mod.Multiplier - 1f) * 100f);
                var sign = percent >= 0 ? "+" : "";
                var color = mod.Multiplier < 1f ? negative : positive;
                var hex = ColorUtility.ToHtmlStringRGB(color);
                if (i > 0) sb.Append(", ");
                sb.Append($"<color=#{hex}>{mod.Genre} {sign}{percent}%</color>");
            }
            return sb.ToString();
        }
    }
}
