using System;
using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Location.UI
{
    /// <summary>
    /// One location row. Unlocked → shows only the Start button; locked → shows the requirement lines
    /// and hides Start. Driven entirely by <see cref="LocationListItemModel"/> (no config/JObject here).
    /// Pooled via <c>UIListPool</c>; <see cref="Cleanup"/> drops the per-row callback on reuse/disable.
    /// </summary>
    public sealed class LocationRowView : MonoBehaviour, ICleanup
    {
        [SerializeField] private TextMeshProUGUI _nameLabel;
        [SerializeField] private GameObject _lockedPanel;
        [SerializeField] private TextMeshProUGUI _conditionsLabel;
        [SerializeField] private Button _startButton;

        private Action<string> _onStart;
        private string _locationId;

        private void Awake()
        {
            if (_startButton != null)
                _startButton.onClick.AddListener(() => _onStart?.Invoke(_locationId));
        }

        public void Bind(LocationListItemModel model, Action<string> onStart)
        {
            _onStart = onStart;
            _locationId = model.LocationId;

            if (_nameLabel != null) _nameLabel.text = model.DisplayName;

            //if (_startButton != null) _startButton.gameObject.SetActive(model.StartEnabled);
            if (_startButton != null) _startButton.interactable = model.StartEnabled;
            if (_lockedPanel != null) _lockedPanel.SetActive(!model.IsUnlocked);
            if (_conditionsLabel != null)
                _conditionsLabel.text = model.IsUnlocked
                    ? string.Empty
                    : string.Join("\n", model.ProgressLines);
        }

        public void Cleanup()
        {
            _onStart = null;
            _locationId = null;
        }
    }
}
