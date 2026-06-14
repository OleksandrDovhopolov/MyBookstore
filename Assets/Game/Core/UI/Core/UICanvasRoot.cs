using UnityEngine;
using VContainer;

namespace Game.UI
{
    public sealed class UICanvasRoot : MonoBehaviour, IUICanvasRoot
    {
        [SerializeField] private Transform _hudRoot;
        [SerializeField] private Transform _windowsRoot;
        [SerializeField] private GameObject _blocker;

        private IObjectResolver _resolver;
        private bool _siblingsInjected;

        public Transform HudRoot => _hudRoot;
        public Transform WindowsRoot => _windowsRoot;
        public GameObject Blocker => _blocker;

        // VContainer's RegisterComponentInNewPrefab injects only this registered component.
        // We cache the resolver here, then re-inject sibling MonoBehaviours in Start()
        // (after the full container resolve chain has completed — avoids circular Resolve).
        [Inject]
        public void Construct(IObjectResolver resolver) => _resolver = resolver;

        private void Awake()
        {
            // VContainer spawns this prefab as an unparented scene root; promote to DontDestroyOnLoad
            // so it survives scene transitions like GlobalLifetimeScope does.
            DontDestroyOnLoad(gameObject);

            if (_blocker != null) _blocker.SetActive(false);
        }

        //TOOD delete this after test. is used to inject UiPilotDebugPanel
        private void Start()
        {
            if (_siblingsInjected || _resolver == null) return;
            _siblingsInjected = true;

            var siblings = GetComponents<MonoBehaviour>();
            for (var i = 0; i < siblings.Length; i++)
            {
                var mb = siblings[i];
                if (mb == null || mb == this) continue;
                _resolver.Inject(mb);
            }
        }
    }
}
