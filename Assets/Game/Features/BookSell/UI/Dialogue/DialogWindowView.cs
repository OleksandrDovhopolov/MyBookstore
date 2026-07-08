using System;
using System.Collections.Generic;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Book.Sell.UI
{
    /// <summary>
    /// Dumb render surface for <see cref="DialogWindow"/>: node lines + up to 3 answer-option buttons.
    /// All flow logic lives in the window controller; this only exposes serialized refs and wires clicks
    /// back through the <c>onPick</c> callback. On a terminal node the window passes a single "Continue"
    /// label (button #0), so one of the option buttons is reused as the close button.
    ///
    /// Prefab is authored by the user: a lines label + up to 3 buttons, each with a TMP_Text child.
    /// <see cref="_optionButtons"/> and <see cref="_optionLabels"/> must be index-aligned.
    /// </summary>
    public sealed class DialogWindowView : WindowView
    {
        [Header("Dialogue lines")]
        [SerializeField] private TMP_Text _linesText;

        [Header("Answer options (index-aligned; up to 3)")]
        [SerializeField] private Button[] _optionButtons;
        [SerializeField] private TMP_Text[] _optionLabels;

        private Action<int> _onPick;

        protected override void Awake()
        {
            base.Awake();

            // Bind each button once to a stable closure carrying its index; the active callback is swapped
            // per node via SetOptions, so we never add/remove listeners on every render.
            if (_optionButtons == null) return;
            for (var i = 0; i < _optionButtons.Length; i++)
            {
                if (_optionButtons[i] == null) continue;
                var index = i;
                _optionButtons[i].onClick.AddListener(() => _onPick?.Invoke(index));
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (_optionButtons == null) return;
            foreach (var button in _optionButtons)
                if (button != null) button.onClick.RemoveAllListeners();
        }

        /// <summary>Renders the node's lines (joined by newlines).</summary>
        public void SetLines(IReadOnlyList<string> lines)
        {
            if (_linesText != null)
                _linesText.text = lines == null ? string.Empty : string.Join("\n", lines);
        }

        /// <summary>
        /// Shows one button per label (up to the available button count), hides the rest, and routes clicks
        /// to <paramref name="onPick"/> with the button index. On a terminal node the window passes a single
        /// "Continue" label, so button #0 becomes the close button.
        /// </summary>
        public void SetOptions(IReadOnlyList<string> labels, Action<int> onPick)
        {
            _onPick = onPick;

            if (_optionButtons == null) return;
            var count = labels?.Count ?? 0;

            for (var i = 0; i < _optionButtons.Length; i++)
            {
                var visible = i < count;
                if (_optionButtons[i] != null)
                    _optionButtons[i].gameObject.SetActive(visible);
                if (visible && _optionLabels != null && i < _optionLabels.Length && _optionLabels[i] != null)
                    _optionLabels[i].text = labels[i] ?? string.Empty;
            }
        }
    }
}
