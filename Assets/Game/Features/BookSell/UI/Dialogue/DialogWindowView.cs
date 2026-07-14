using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI;
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
    /// Prefab (authored by the user): a ScrollRect whose content hosts two stacked sub-containers — a lines
    /// root for <see cref="_linePool"/> (<see cref="DialogLineView"/>) and an <see cref="_optionsRoot"/> for
    /// <see cref="_buttonPool"/> (<see cref="DialogOptionView"/>) below it — plus a full-screen
    /// <see cref="_clickCatcher"/> button and a <see cref="_skipButton"/>. Each pool's prefab + parent are
    /// assigned in the inspector; the click catcher is disabled while options are shown so the in-feed option
    /// buttons receive clicks.
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

        [Header("Answer options (pooled, in-feed)")]
        [Tooltip("Option-button prefab + parent (OptionsRoot) are assigned on the pool in the inspector.")]
        [SerializeField] private UIListPool<DialogOptionView> _buttonPool = new();
        [Tooltip("The OptionsRoot container (a layout child of the scroll content, below the lines root).")]
        [SerializeField] private GameObject _optionsRoot;

        private DialogClickCatcherGestureBridge _clickCatcherGestureBridge;

        /// <summary>Player clicked the screen (request the next reply / close at the end).</summary>
        public event Action ScreenClicked;

        /// <summary>Player pressed Skip (no-op this iteration).</summary>
        public event Action SkipClicked;

        public Button SkipButton => _skipButton;

        protected override void Awake()
        {
            base.Awake();
            if (_clickCatcher != null)
            {
                _clickCatcherGestureBridge = _clickCatcher.GetComponent<DialogClickCatcherGestureBridge>();
                if (_clickCatcherGestureBridge == null)
                    _clickCatcherGestureBridge = _clickCatcher.gameObject.AddComponent<DialogClickCatcherGestureBridge>();

                _clickCatcherGestureBridge.Configure(_scrollRect, RaiseScreenClicked);
            }

            if (_skipButton != null) _skipButton.onClick.AddListener(RaiseSkipClicked);

            HideOptions();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_clickCatcher != null) _clickCatcher.onClick.RemoveListener(RaiseScreenClicked);
            if (_clickCatcherGestureBridge != null) _clickCatcherGestureBridge.Clear();
            if (_skipButton != null) _skipButton.onClick.RemoveListener(RaiseSkipClicked);
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

        /// <summary>Snaps to the bottom after Unity has had a chance to resolve nested layout/content fitters.</summary>
        public async UniTask ScrollToBottomAfterLayoutAsync(CancellationToken ct)
        {
            try
            {
                ScrollToBottom();
                await UniTask.NextFrame(ct);
                ScrollToBottom();
                await UniTask.NextFrame(ct);
                ScrollToBottom();
            }
            catch (OperationCanceledException)
            {
                // Window is closing while the layout pass is pending.
            }
        }

        /// <summary>Clears the feed (returns all pooled replies).</summary>
        public void ClearLines() => _linePool.DisableAll();

        /// <summary>Spawns one pooled <see cref="DialogOptionView"/> per label (in the OptionsRoot, below the
        /// reply feed) and routes clicks to <paramref name="onPick"/> with the option index. The click catcher
        /// is disabled while options are up so the in-feed buttons receive clicks — but only when at least one
        /// button is actually shown (fail-soft for empty/broken content).</summary>
        public void ShowOptions(IReadOnlyList<string> labels, Action<int> onPick)
        {
            _buttonPool.DisableAll();

            var count = labels?.Count ?? 0;
            for (var i = 0; i < count; i++)
                _buttonPool.GetNext().Bind(i, labels[i], onPick);

            if (_optionsRoot != null) _optionsRoot.SetActive(count > 0);
            if (count > 0 && _clickCatcher != null) _clickCatcher.gameObject.SetActive(false);
        }

        /// <summary>Clears the option buttons, hides the OptionsRoot, and re-enables the click catcher.
        /// Null-safe and idempotent — safe to call from teardown/reset, not only while the window is open.</summary>
        public void HideOptions()
        {
            _buttonPool.DisableAll();
            if (_optionsRoot != null) _optionsRoot.SetActive(false);
            if (_clickCatcher != null) _clickCatcher.gameObject.SetActive(true);
        }

        private void ScrollToBottom()
        {
            if (_scrollRect == null) return;
            // Rebuild layout so the new reply's size is accounted for before snapping to the bottom.
            Canvas.ForceUpdateCanvases();
            if (_scrollRect.content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollRect.content);
            Canvas.ForceUpdateCanvases();
            _scrollRect.velocity = Vector2.zero;
            _scrollRect.verticalNormalizedPosition = 0f;
        }

        private void RaiseScreenClicked() => ScreenClicked?.Invoke();
        private void RaiseSkipClicked() => SkipClicked?.Invoke();
    }
}
