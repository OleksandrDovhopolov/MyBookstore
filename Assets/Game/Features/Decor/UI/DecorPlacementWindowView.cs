using Game.UI;
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
        [Tooltip("Optional full-screen transparent button behind the HUD; clicking it closes the HUD.")]
        [SerializeField] private Button _hudBackdrop;
        [SerializeField] private Button _replaceButton; // MVP: visible but disabled
        [SerializeField] private Button _removeButton;

        [Header("Audio")]
        [Tooltip("Optional. Played on a successful place; leave empty for silence until a clip exists.")]
        [SerializeField] private AudioClip _placeClip;
        [Tooltip("Optional. Played on a successful remove; leave empty for silence until a clip exists.")]
        [SerializeField] private AudioClip _removeClip;

        public RectTransform RoomImageRect => _roomImageRect;
        public DecorSlotAnchorView[] SlotAnchors => _slotAnchors;

        public UIListPool<DecorInventoryCardView> CardsPool => _cardsPool;

        public GameObject SelectedSlotHud => _selectedSlotHud;
        public Button HudBackdrop => _hudBackdrop;
        public Button ReplaceButton => _replaceButton;
        public Button RemoveButton => _removeButton;

        public AudioClip PlaceClip => _placeClip;
        public AudioClip RemoveClip => _removeClip;
    }
}
