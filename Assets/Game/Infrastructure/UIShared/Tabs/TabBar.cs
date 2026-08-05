using System;
using System.Collections.Generic;
using UnityEngine;

namespace UIShared
{
    /// <summary>
    /// Group of tab buttons with single selection. Mutual exclusion is done here rather than by a
    /// ToggleGroup, so buttons can be listed in any order and matching is by tab value, not index.
    /// <see cref="SelectTab"/> deliberately does not raise <see cref="Selected"/> — only a user click
    /// does, so a window applying its remembered tab cannot re-enter its own refresh.
    /// </summary>
    public abstract class TabBar<TEnum> : MonoBehaviour
        where TEnum : struct, Enum
    {
        [SerializeField] private TabButton<TEnum>[] _buttons = Array.Empty<TabButton<TEnum>>();

        public TEnum? ActiveTab { get; private set; }

        public event Action<TEnum> Selected;

        protected virtual void Awake()
        {
            if (_buttons == null)
                return;

            for (var i = 0; i < _buttons.Length; i++)
            {
                var button = _buttons[i];
                if (button != null)
                    button.Selected += OnButtonSelected;
            }
        }

        public void SelectTab(TEnum tab)
        {
            ActiveTab = tab;

            if (_buttons == null)
                return;

            var comparer = EqualityComparer<TEnum>.Default;
            for (var i = 0; i < _buttons.Length; i++)
            {
                var button = _buttons[i];
                if (button != null)
                    button.SetSelected(comparer.Equals(button.Tab, tab));
            }
        }

        private void OnButtonSelected(TEnum tab)
        {
            SelectTab(tab);
            Selected?.Invoke(tab);
        }

        protected virtual void OnDestroy()
        {
            if (_buttons == null)
                return;

            for (var i = 0; i < _buttons.Length; i++)
            {
                var button = _buttons[i];
                if (button != null)
                    button.Selected -= OnButtonSelected;
            }
        }
    }
}
