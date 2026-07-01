using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Decor.UI
{
    /// <summary>Read-only info popup for a decor: icon, name, bonuses, description, single close button.</summary>
    public sealed class DecorInfoPopupView : WindowView
    {
        [Header("Content")]
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _nameLabel;
        [SerializeField] private TextMeshProUGUI _bonusesLabel;
        [SerializeField] private TextMeshProUGUI _descriptionLabel;

        [Header("Footer")]
        [SerializeField] private Button _closeButton;

        [Header("Colors")]
        [SerializeField] private Color _positiveColor = new(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color _negativeColor = new(0.9f, 0.25f, 0.25f);

        public Image Icon => _icon;
        public TextMeshProUGUI NameLabel => _nameLabel;
        public TextMeshProUGUI BonusesLabel => _bonusesLabel;
        public TextMeshProUGUI DescriptionLabel => _descriptionLabel;
        public Button CloseButton => _closeButton;

        public Color PositiveColor => _positiveColor;
        public Color NegativeColor => _negativeColor;
    }
}
