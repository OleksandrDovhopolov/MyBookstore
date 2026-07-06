using UnityEngine;

namespace Infrastructure.ResourceAnimations
{
    /// <summary>
    /// Registers this GameObject's <see cref="RectTransform"/> as a resource animation target while enabled.
    /// Drop it on a UI target and set the id, for example "resource:Gold".
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class ResourceAnimationTargetTag : MonoBehaviour
    {
        [Tooltip("Target id, e.g. \"resource:Gold\".")]
        [SerializeField] private string _targetId;

        private RectTransform _rt;
        private RectTransform RectTransform => _rt != null ? _rt : _rt = (RectTransform)transform;

        private void OnEnable()
        {
            if (!string.IsNullOrWhiteSpace(_targetId))
                ResourceAnimationTargets.Register(_targetId, RectTransform);
        }

        private void OnDisable()
        {
            if (!string.IsNullOrWhiteSpace(_targetId))
                ResourceAnimationTargets.Unregister(_targetId, RectTransform);
        }
    }
}
