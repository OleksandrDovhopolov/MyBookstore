using System;
using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Dialogue
{
    /// <summary>
    /// One answer-option button in the dialogue feed (GAME-6): a button + its label, pooled via
    /// <see cref="UIShared.UIListPool{T}"/> inside the ScrollRect content (below the reply lines) so options
    /// flow with the feed. Dumb view — it only renders itself and forwards the click with its index; the flow
    /// (which options, when, what a pick does) lives in <see cref="DialogWindow"/>.
    ///
    /// Implements <see cref="ICleanup"/> so the pool resets a reused instance (no stale label / callback).
    /// </summary>
    public sealed class DialogOptionView : MonoBehaviour, ICleanup
    {
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _label;

        private int _index;
        private Action<int> _onPick;
        private UnityAction _clickHandler;

        private void Awake()
        {
            // Bind once to a stable handler that reads the current index/callback; the pool swaps those per
            // render, so we never add/remove listeners on every reuse.
            _clickHandler = () => _onPick?.Invoke(_index);
            if (_button != null) _button.onClick.AddListener(_clickHandler);
        }

        public void Bind(int index, string text, Action<int> onPick)
        {
            _index = index;
            _onPick = onPick;

            if (_label != null) _label.text = text ?? string.Empty;
            // The pool re-activates the GameObject, but a recycled instance may have been left non-interactable
            // by future logic — reset explicitly.
            if (_button != null) _button.interactable = true;
        }

        public void Cleanup()
        {
            _onPick = null;
            if (_label != null) _label.text = string.Empty;
        }

        private void OnDestroy()
        {
            // Remove ONLY our own listener — RemoveAllListeners would wipe any listeners a designer added on
            // the prefab.
            if (_button != null && _clickHandler != null) _button.onClick.RemoveListener(_clickHandler);
        }
    }
}
