using Game.UI;
using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Decor.UI
{
    /// <summary>
    /// Visual layout for <see cref="DecorPlacementWindow"/> (MVP): a room background with UI slot
    /// anchors, a bottom inventory panel of decor cards, a selected-slot HUD (Replace/Remove) and
    /// an info popup. Replaces the earlier debug/list layout. Slot anchors are authored in the
    /// prefab and linked to config by their string slot id (see <see cref="DecorSlotAnchorView"/>).
    /// </summary>
    public sealed class DecorPlacementWindowView : WindowView
    {
        [Header("Room")]
        [Tooltip("Container matching the VISIBLE room image; slot anchors live under it so they stay put across aspect ratios.")]
        [SerializeField] private RectTransform _roomImageRect;
        [SerializeField] private DecorSlotAnchorView[] _slotAnchors;

        [Header("Bottom inventory panel")]
        [Tooltip("Card prefab + parent are assigned on the pool in the inspector.")]
        [SerializeField] private UIListPool<DecorInventoryCardView> _cardsPool = new();

        [Header("Selected-slot HUD")]
        [SerializeField] private GameObject _selectedSlotHud;
        [SerializeField] private Button _replaceButton; // MVP: visible but disabled
        [SerializeField] private Button _removeButton;

        [Header("Info popup")]
        [SerializeField] private GameObject _infoPopupRoot;
        [SerializeField] private Image _infoIcon;
        [SerializeField] private TextMeshProUGUI _infoNameLabel;
        [SerializeField] private TextMeshProUGUI _infoBonusesLabel;
        [SerializeField] private TextMeshProUGUI _infoDescriptionLabel;
        [SerializeField] private Button _infoCloseButton;

        [Header("Footer")]
        [SerializeField] private Button _closeButton;

        [Header("Audio")]
        [SerializeField] private AudioClip _placeClip;

        [Header("Colors")]
        [SerializeField] private Color _positiveColor = new(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color _negativeColor = new(0.9f, 0.25f, 0.25f);

        public RectTransform RoomImageRect => _roomImageRect;
        public DecorSlotAnchorView[] SlotAnchors => _slotAnchors;

        public UIListPool<DecorInventoryCardView> CardsPool => _cardsPool;

        public GameObject SelectedSlotHud => _selectedSlotHud;
        public Button ReplaceButton => _replaceButton;
        public Button RemoveButton => _removeButton;

        public GameObject InfoPopupRoot => _infoPopupRoot;
        public Image InfoIcon => _infoIcon;
        public TextMeshProUGUI InfoNameLabel => _infoNameLabel;
        public TextMeshProUGUI InfoBonusesLabel => _infoBonusesLabel;
        public TextMeshProUGUI InfoDescriptionLabel => _infoDescriptionLabel;
        public Button InfoCloseButton => _infoCloseButton;

        public Button CloseButton => _closeButton;

        public AudioClip PlaceClip => _placeClip;

        public Color PositiveColor => _positiveColor;
        public Color NegativeColor => _negativeColor;
    }
}
