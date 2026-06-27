using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Location.UI
{
    /// <summary>
    /// One location row. Unlocked → shows only the Start button; locked → shows the requirement lines
    /// and hides Start. Driven entirely by <see cref="LocationListItemModel"/> (no config/JObject here).
    /// </summary>
    public sealed class LocationRowView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _nameLabel;
        [SerializeField] private GameObject _lockedPanel;
        [SerializeField] private TextMeshProUGUI _conditionsLabel;
        [SerializeField] private Button _startButton;

        private Action _onStart;

        private void Awake()
        {
            if (_startButton != null)
                _startButton.onClick.AddListener(() => _onStart?.Invoke());
        }

        public void Bind(LocationListItemModel model, Action onStart)
        {
            _onStart = onStart;

            if (_nameLabel != null) _nameLabel.text = model.DisplayName;

            if (_startButton != null) _startButton.gameObject.SetActive(model.StartEnabled);
            if (_lockedPanel != null) _lockedPanel.SetActive(!model.IsUnlocked);
            if (_conditionsLabel != null)
                _conditionsLabel.text = model.IsUnlocked
                    ? string.Empty
                    : string.Join("\n", model.ProgressLines);
        }
    }
}
