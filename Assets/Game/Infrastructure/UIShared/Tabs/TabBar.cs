using System;
using UnityEngine;

namespace UIShared
{
    public sealed class TabBar : MonoBehaviour
    {
        [SerializeField] private TabButton[] _buttons = Array.Empty<TabButton>();

        public TabType? ActiveTab { get; private set; }

        public event Action<TabType> Selected;

        private void Awake()
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

        public void SelectTab(TabType tab)
        {
            ActiveTab = tab;

            if (_buttons == null)
                return;

            for (var i = 0; i < _buttons.Length; i++)
            {
                var button = _buttons[i];
                if (button != null)
                    button.SetSelected(button.Tab == tab);
            }
        }

        private void OnButtonSelected(TabType tab)
        {
            SelectTab(tab);
            Selected?.Invoke(tab);
        }

        private void OnDestroy()
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
