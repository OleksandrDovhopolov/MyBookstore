using UnityEngine;

namespace Infrastructure.TutorialUI
{
    /// <summary>
    /// Registers this GameObject's <see cref="RectTransform"/> as a tutorial highlight target under
    /// <see cref="_targetId"/> while enabled, and unregisters on disable — so additive scene load/unload is
    /// handled for free. Drop on any button/panel the tutorial may point at and set the id (see
    /// because scene/prefab objects do not receive DI injection.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class TutorialTargetTag : MonoBehaviour
    {
        [Tooltip("Target id (see TutorialTargetIds), e.g. \"hub.start_day_button\".")]
        [TutorialTargetId]
        [SerializeField] private string _targetId;

        private RectTransform _rt;
        private RectTransform RectTransform => _rt != null ? _rt : _rt = (RectTransform)transform;

        private void OnEnable()
        {
            if (!string.IsNullOrWhiteSpace(_targetId))
                TutorialTargets.Register(_targetId, RectTransform);
        }

        private void OnDisable()
        {
            if (!string.IsNullOrWhiteSpace(_targetId))
                TutorialTargets.Unregister(_targetId, RectTransform);
        }
    }
}
