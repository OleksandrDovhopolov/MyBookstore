using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Journal.UI
{
    public sealed class JournalTabButton : MonoBehaviour
    {
        [SerializeField] private JournalTab _tab;
        [SerializeField] private Toggle _toggle;

        public event Action<JournalTab> Selected;

        private void Awake()
        {
            if (_toggle == null) _toggle = GetComponent<Toggle>();
            if (_toggle != null) _toggle.onValueChanged.AddListener(OnValueChanged);
        }

        public void SetSelected(bool selected)
        {
            if (_toggle != null) _toggle.SetIsOnWithoutNotify(selected);
        }

        private void OnValueChanged(bool value)
        {
            if (value) Selected?.Invoke(_tab);
        }

        private void OnDestroy()
        {
            if (_toggle != null) _toggle.onValueChanged.RemoveListener(OnValueChanged);
        }
    }
}
