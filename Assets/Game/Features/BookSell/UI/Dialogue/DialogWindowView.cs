using System;
using System.Collections.Generic;
using Game.UI;
using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Book.Sell.UI
{
    /// <summary>
    /// Dumb render surface for <see cref="DialogWindow"/> (GAME-6): a top-to-bottom feed of reply views, a
    /// full-screen click catcher, a Skip button, and an answer-options panel. Replies are pooled
    /// (<see cref="UIShared.UIListPool{T}"/>) and appended downward; the feed auto-scrolls to the newest. The
    /// options panel is shown when the current node offers a choice. All flow logic (which reply is next,
    /// blocking clicks while typing, when to offer options) lives in the window controller — this only
    /// appends, scrolls, toggles options, and forwards the click/skip/option events.
    ///
    /// Prefab (authored by the user): a ScrollRect whose content hosts the pool, a <see cref="DialogLineView"/>
    /// prefab, a full-screen <see cref="_clickCatcher"/> button, a <see cref="_skipButton"/>, and an
    /// <see cref="_optionsPanel"/> with index-aligned <see cref="_optionButtons"/>/<see cref="_optionLabels"/>
    /// placed ABOVE the click catcher (so option clicks register). Pool's prefab + parent are assigned on
    /// <see cref="_linePool"/> in the inspector.
    /// </summary>
    public sealed class DialogWindowView : WindowView
    {
        [Header("Reply feed")]
        [Tooltip("Line prefab + content parent are assigned on the pool in the inspector.")]
        [SerializeField] private UIListPool<DialogLineView> _linePool = new();
        [SerializeField] private ScrollRect _scrollRect;

        [Header("Input")]
        [SerializeField] private Button _clickCatcher;   // full-screen: advance to the next reply
        [SerializeField] private Button _skipButton;

        [Header("Answer options (index-aligned)")]
        [SerializeField] private GameObject _optionsPanel;
        [SerializeField] private Button[] _optionButtons;
        [SerializeField] private TMP_Text[] _optionLabels;

        private Action<int> _onOptionPick;

        /// <summary>Player clicked the screen (request the next reply / close at the end).</summary>
        public event Action ScreenClicked;

        /// <summary>Player pressed Skip (no-op this iteration).</summary>
        public event Action SkipClicked;

        public Button SkipButton => _skipButton;

        protected override void Awake()
        {
            base.Awake();
            if (_clickCatcher != null) _clickCatcher.onClick.AddListener(RaiseScreenClicked);
            if (_skipButton != null) _skipButton.onClick.AddListener(RaiseSkipClicked);

            // Bind option buttons once to a stable index closure; the active callback is swapped per node via
            // ShowOptions, so we never add/remove listeners on every render.
            if (_optionButtons != null)
            {
                for (var i = 0; i < _optionButtons.Length; i++)
                {
                    if (_optionButtons[i] == null) continue;
                    var index = i;
                    _optionButtons[i].onClick.AddListener(() => _onOptionPick?.Invoke(index));
                }
            }

            HideOptions();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_clickCatcher != null) _clickCatcher.onClick.RemoveListener(RaiseScreenClicked);
            if (_skipButton != null) _skipButton.onClick.RemoveListener(RaiseSkipClicked);

            if (_optionButtons != null)
                foreach (var button in _optionButtons)
                    if (button != null) button.onClick.RemoveAllListeners();
        }

        /// <summary>Appends a reply at the bottom of the feed (aligned to <paramref name="isRight"/>) and
        /// scrolls to it. Returns the view so the controller can run its appear + typewriter reveal.</summary>
        public DialogLineView AppendLine(string speaker, string text, bool isRight)
        {
            var line = _linePool.GetNext();
            line.SetSide(isRight);
            line.Bind(speaker, text);
            ScrollToBottom();
            return line;
        }

        /// <summary>Clears the feed (returns all pooled replies).</summary>
        public void ClearLines() => _linePool.DisableAll();

        /// <summary>Shows one button per label (clamped to the available buttons) and routes clicks to
        /// <paramref name="onPick"/> with the button index. Extra labels beyond the prefab's buttons are
        /// dropped with a warning.</summary>
        public void ShowOptions(IReadOnlyList<string> labels, Action<int> onPick)
        {
            _onOptionPick = onPick;

            var buttonCount = _optionButtons?.Length ?? 0;
            var wanted = labels?.Count ?? 0;
            var visible = Mathf.Min(wanted, buttonCount, _optionLabels?.Length ?? 0);
            if (wanted > visible)
                Debug.LogWarning($"[DialogWindowView] {wanted} option(s) but only {visible} button(s) — extras dropped.");

            for (var i = 0; i < buttonCount; i++)
            {
                var show = i < visible;
                if (_optionButtons[i] != null) _optionButtons[i].gameObject.SetActive(show);
                if (show && _optionLabels != null && i < _optionLabels.Length && _optionLabels[i] != null)
                    _optionLabels[i].text = labels[i] ?? string.Empty;
            }

            if (_optionsPanel != null) _optionsPanel.SetActive(visible > 0);
        }

        /// <summary>Hides the options panel.</summary>
        public void HideOptions()
        {
            _onOptionPick = null;
            if (_optionsPanel != null) _optionsPanel.SetActive(false);
        }

        private void ScrollToBottom()
        {
            if (_scrollRect == null) return;
            // Rebuild layout so the new reply's size is accounted for before snapping to the bottom.
            Canvas.ForceUpdateCanvases();
            _scrollRect.verticalNormalizedPosition = 0f;
        }

        private void RaiseScreenClicked() => ScreenClicked?.Invoke();
        private void RaiseSkipClicked() => SkipClicked?.Invoke();
    }
}
