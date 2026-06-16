using System;
using Game.Inventory.API;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Inventory.UI
{
    /// <summary>
    /// Single row in the debug inventory list. Bound by <see cref="InventoryScreenView"/>; one
    /// instantiated per item.
    /// </summary>
    public sealed class InventoryItemRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _idLabel;
        [SerializeField] private TMP_Text _countLabel;
        [SerializeField] private Button _useButton;

        private string _itemId;
        private Action<string> _onUse;

        private void Awake()
        {
            if (_useButton != null) _useButton.onClick.AddListener(OnUseClicked);
        }

        public void Bind(InventoryItem item, bool hasUseHandler, string info, Action<string> onUse)
        {
            _itemId = item.ItemId;
            _onUse = onUse;
            if (_idLabel != null)
                _idLabel.text = string.IsNullOrEmpty(info) ? item.ItemId : $"{item.ItemId} — {info}";
            if (_countLabel != null)
            {
                _countLabel.gameObject.SetActive(item.Count > 1);
                _countLabel.text = $"×{item.Count}";
            }
        }

        private void OnUseClicked()
        {
            if (!string.IsNullOrEmpty(_itemId)) _onUse?.Invoke(_itemId);
        }

        private void OnDestroy()
        {
            if (_useButton != null) _useButton.onClick.RemoveListener(OnUseClicked);
        }
    }
}
