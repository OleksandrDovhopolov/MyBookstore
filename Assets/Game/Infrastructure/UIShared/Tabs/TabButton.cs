using System;
using UnityEngine;
using UnityEngine.UI;

namespace UIShared
{
    /// <summary>
    /// Toggle-backed tab button. Owns only the tab value and the toggle bridge; how the selected
    /// state looks is delegated to <see cref="ITabButtonVisual"/> components on the same GameObject.
    /// Generic over the tab enum so each window keeps its own set of tabs — Unity cannot attach a
    /// generic MonoBehaviour, so every feature declares a concrete subclass (see <c>GameTabButton</c>).
    /// </summary>
    public abstract class TabButton<TEnum> : MonoBehaviour
        where TEnum : struct, Enum
    {
        [SerializeField] private TEnum _tab;
        [SerializeField] private Toggle _toggle;

        private ITabButtonVisual[] _visuals;

        public TEnum Tab => _tab;

        public event Action<TEnum> Selected;

        protected virtual void Awake()
        {
            if (_toggle == null)
                _toggle = GetComponent<Toggle>();

            if (_toggle != null)
                _toggle.onValueChanged.AddListener(OnValueChanged);

            _visuals = GetComponents<ITabButtonVisual>();
        }

        public void SetSelected(bool selected)
        {
            if (_toggle != null)
                _toggle.SetIsOnWithoutNotify(selected);

            // Defensive: SetSelected can arrive before Awake if a window binds during its own Awake.
            _visuals ??= GetComponents<ITabButtonVisual>();

            for (var i = 0; i < _visuals.Length; i++)
                _visuals[i]?.ApplySelected(selected);
        }

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
