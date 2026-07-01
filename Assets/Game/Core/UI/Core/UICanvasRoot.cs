using UnityEngine;

namespace Game.UI
{
    public sealed class UICanvasRoot : MonoBehaviour, IUICanvasRoot
    {
        [SerializeField] private Transform _hudRoot;
        [SerializeField] private Transform _windowsRoot;
        [SerializeField] private GameObject _blocker;
        [SerializeField] private MonoBehaviour _transitionAnimation;

        public Transform HudRoot => _hudRoot;
        public Transform WindowsRoot => _windowsRoot;
        public GameObject Blocker => _blocker;
        public MonoBehaviour TransitionAnimation => _transitionAnimation;

        // Keep the sibling injection experiment disabled for now. It was only needed for the old
        // UiPilotDebugPanel, and injecting arbitrary components from the canvas root can re-enter
        // VContainer's singleton graph during boot.
        // private IObjectResolver _resolver;
        // private bool _siblingsInjected;
        //
        // [Inject]
        // public void Construct(IObjectResolver resolver) => _resolver = resolver;

        private void Awake()
        {
            // VContainer spawns this prefab as an unparented scene root; promote to DontDestroyOnLoad
            // so it survives scene transitions like GlobalLifetimeScope does.
            DontDestroyOnLoad(gameObject);

            if (_blocker != null) _blocker.SetActive(false);
        }

        // private void Start()
        // {
        //     if (_siblingsInjected || _resolver == null) return;
        //     _siblingsInjected = true;
        //
        //     var siblings = GetComponents<MonoBehaviour>();
        //     for (var i = 0; i < siblings.Length; i++)
        //     {
        //         var mb = siblings[i];
        //         if (mb == null || mb == this) continue;
        //         _resolver.Inject(mb);
        //     }
        // }
    }
}
