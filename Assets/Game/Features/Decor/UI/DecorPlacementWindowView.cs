using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Decor.UI
{
    public sealed class DecorPlacementWindowView : WindowView
    {
        [Header("Summary panel")]
        [SerializeField] private TextMeshProUGUI _summaryLabel;
        [SerializeField] private TextMeshProUGUI _capHintLabel;

        [Header("Lists")]
        [SerializeField] private Transform _slotListRoot;
        [SerializeField] private Transform _inventoryListRoot;

        [Header("Templates (single child each)")]
        [SerializeField] private DecorSlotRowView _slotRowTemplate;
        [SerializeField] private DecorInventoryRowView _inventoryRowTemplate;

        [Header("Footer buttons")]
        [SerializeField] private Button _clearAllButton;
        [SerializeField] private Button _closeButton;

        [Header("Colors")]
        [SerializeField] private Color _positiveColor = new(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color _negativeColor = new(0.9f, 0.25f, 0.25f);
        [SerializeField] private Color _capHintColor = new(0.95f, 0.85f, 0.2f);

        public TextMeshProUGUI SummaryLabel => _summaryLabel;
        public TextMeshProUGUI CapHintLabel => _capHintLabel;

        public Transform SlotListRoot => _slotListRoot;
        public Transform InventoryListRoot => _inventoryListRoot;

        public DecorSlotRowView SlotRowTemplate => _slotRowTemplate;
        public DecorInventoryRowView InventoryRowTemplate => _inventoryRowTemplate;

        public Button ClearAllButton => _clearAllButton;
        public Button CloseButton => _closeButton;

        public Color PositiveColor => _positiveColor;
        public Color NegativeColor => _negativeColor;
        public Color CapHintColor => _capHintColor;
    }
}
