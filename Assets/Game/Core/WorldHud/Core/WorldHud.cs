using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.WorldHud
{
    // Abstract base for any world-space HUD attached to a scene Transform.
    // Subclass-specific content lives in derived MonoBehaviour fields/components on the same prefab.
    public abstract class WorldHud : MonoBehaviour, IWorldHud
    {
        [SerializeField] private CanvasGroup _canvasGroup;

        private Transform _target;
        private WorldHudArgs _args;
        private Camera _camera;
        private bool _isAttached;
        private bool _suppressed;
        private float _alphaBeforeSuppress;

        public Transform Target => _target;
        public WorldHudArgs Args => _args;
        public bool IsAttached => _isAttached;
        public GameObject GameObject => gameObject;

        protected CanvasGroup CanvasGroup => _canvasGroup;

        // Called by WorldHudManager right after Instantiate + Inject.
        internal void AttachInternal(Transform target, WorldHudArgs args, Camera camera)
        {
            _target = target;
            _args = args;
            _camera = camera;
            _isAttached = true;

            if (_canvasGroup != null) _canvasGroup.alpha = 0f;

            // Place at correct position immediately so the first frame doesn't flash at origin.
            SnapToTarget();

            OnAttached();
        }

        // Manager wires DetachAsync to this. Subclasses override OnDetachAsync for fade-out etc.
        public async UniTask DetachAsync(CancellationToken ct = default)
        {
            if (!_isAttached) return;

            try { await OnDetachAsync(ct); }
            catch (System.OperationCanceledException) { /* swallow */ }

            _isAttached = false;
            if (this != null && gameObject != null) Destroy(gameObject);
        }

        // Globally hides/shows this HUD (e.g. while a focused window is up) without detaching it.
        // Idempotent; saves the current alpha on suppress and restores it on release so the HUD reappears
        // exactly as it was. Driven by WorldHudManager.SuppressAll().
        public void SetGloballySuppressed(bool suppressed)
        {
            if (_canvasGroup == null || suppressed == _suppressed) return;
            _suppressed = suppressed;

            if (suppressed)
            {
                _alphaBeforeSuppress = _canvasGroup.alpha;
                _canvasGroup.alpha = 0f;
            }
            else
            {
                _canvasGroup.alpha = _alphaBeforeSuppress;
            }
        }

        protected virtual void OnAttached() { }
        protected virtual UniTask OnDetachAsync(CancellationToken ct) => UniTask.CompletedTask;

        protected virtual void LateUpdate()
        {
            if (!_isAttached || _target == null) return;
            SnapToTarget();
        }

        private void SnapToTarget()
        {
            transform.position = _target.position + _args.Offset;

            if (_args.Billboard && _camera != null)
            {
                transform.rotation = _camera.transform.rotation;
            }
        }
    }
}
