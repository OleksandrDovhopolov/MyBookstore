using System;
using UnityEngine;
using UnityEngine.UI;

namespace UIShared
{
    public abstract class TabButton : MonoBehaviour
    {
        [SerializeField] private TabType _tab;
        [SerializeField] private Toggle _toggle;

        public TabType Tab => _tab;

        public event Action<TabType> Selected;

        protected virtual void Awake()
        {
            if (_toggle == null)
                _toggle = GetComponent<Toggle>();

            if (_toggle != null)
                _toggle.onValueChanged.AddListener(OnValueChanged);
        }

        public void SetSelected(bool selected)
        {
            if (_toggle != null)
                _toggle.SetIsOnWithoutNotify(selected);

            ApplySelected(selected);
        }

        protected abstract void ApplySelected(bool selected);

        private void OnValueChanged(bool value)
        {
            if (value)
                Selected?.Invoke(_tab);
        }

        protected virtual void OnDestroy()
        {
            if (_toggle != null)
                _toggle.onValueChanged.RemoveListener(OnValueChanged);
        }
    }
}
