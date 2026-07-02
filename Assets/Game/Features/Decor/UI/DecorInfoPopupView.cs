using Game.UI;
using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Decor.UI
{
    /// <summary>Read-only info popup for a decor: icon, name, description, a scroll of bonus rows and
    /// a scroll of characteristic chips, single close button.</summary>
    public sealed class DecorInfoPopupView : WindowView
    {
        [Header("Content")]
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _nameLabel;
        [SerializeField] private TextMeshProUGUI _descriptionLabel;

        [Header("Bonuses scroll (icon + description + percent)")]
        [Tooltip("Row prefab + scroll content parent are assigned on the pool in the inspector.")]
        [SerializeField] private UIListPool<DecorBonusItemView> _bonusesPool = new();
        [Tooltip("Placeholder icon used for every bonus row until per-bonus Addressable icons exist.")]
        [SerializeField] private Sprite _bonusIconPlaceholder;

        [Header("Characteristics scroll (icon + label)")]
        [Tooltip("Chip prefab + scroll content parent are assigned on the pool in the inspector.")]
        [SerializeField] private UIListPool<DecorCharacteristicItemView> _characteristicsPool = new();
        [Tooltip("Placeholder icon used for every characteristic chip until per-tag Addressable icons exist.")]
        [SerializeField] private Sprite _characteristicIconPlaceholder;

        [Header("Colors")]
        [SerializeField] private Color _positiveColor = new(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color _negativeColor = new(0.9f, 0.25f, 0.25f);

        public Image Icon => _icon;
        public TextMeshProUGUI NameLabel => _nameLabel;
        public TextMeshProUGUI DescriptionLabel => _descriptionLabel;

        public UIListPool<DecorBonusItemView> BonusesPool => _bonusesPool;
        public Sprite BonusIconPlaceholder => _bonusIconPlaceholder;

        public UIListPool<DecorCharacteristicItemView> CharacteristicsPool => _characteristicsPool;
        public Sprite CharacteristicIconPlaceholder => _characteristicIconPlaceholder;

        public Color PositiveColor => _positiveColor;
        public Color NegativeColor => _negativeColor;
    }
}
