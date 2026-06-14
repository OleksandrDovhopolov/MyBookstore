using UnityEngine;

namespace Game.UI
{
    public sealed class UICanvasRoot : MonoBehaviour, IUICanvasRoot
    {
        [SerializeField] private Transform _hudRoot;
        [SerializeField] private Transform _windowsRoot;
        [SerializeField] private GameObject _blocker;

        public Transform HudRoot => _hudRoot;
        public Transform WindowsRoot => _windowsRoot;
        public GameObject Blocker => _blocker;

        private void Awake()
        {
            // VContainer spawns this prefab as an unparented scene root; promote to DontDestroyOnLoad
            // so it survives scene transitions like GlobalLifetimeScope does.
            DontDestroyOnLoad(gameObject);

            if (_blocker != null) _blocker.SetActive(false);
        }
    }
}
