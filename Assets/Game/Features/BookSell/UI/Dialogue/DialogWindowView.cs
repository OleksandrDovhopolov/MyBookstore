using System;
using Game.UI;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Book.Sell.UI
{
    /// <summary>
    /// Dumb render surface for <see cref="DialogWindow"/> (GAME-6): a top-to-bottom feed of reply views plus
    /// a full-screen click catcher and a Skip button. Replies are pooled (<see cref="UIShared.UIListPool{T}"/>)
    /// and appended downward; the feed auto-scrolls to the newest. All flow logic (which reply is next,
    /// blocking clicks while typing) lives in the window controller — this only appends, scrolls, and forwards
    /// the click/skip events.
    ///
    /// Prefab (authored by the user): a ScrollRect whose content hosts the pool, a <see cref="DialogLineView"/>
    /// prefab, a full-screen <see cref="_clickCatcher"/> button, and a <see cref="_skipButton"/> above it.
    /// Pool's prefab + parent (the scroll content) are assigned on <see cref="_linePool"/> in the inspector.
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
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_clickCatcher != null) _clickCatcher.onClick.RemoveListener(RaiseScreenClicked);
            if (_skipButton != null) _skipButton.onClick.RemoveListener(RaiseSkipClicked);
        }

        /// <summary>Appends a reply at the bottom of the feed and scrolls to it. Returns the view so the
        /// controller can run its typewriter reveal.</summary>
        public DialogLineView AppendLine(string speaker, string text)
        {
            var line = _linePool.GetNext();
            line.Bind(speaker, text);
            ScrollToBottom();
            return line;
        }

        /// <summary>Clears the feed (returns all pooled replies).</summary>
        public void ClearLines() => _linePool.DisableAll();

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
