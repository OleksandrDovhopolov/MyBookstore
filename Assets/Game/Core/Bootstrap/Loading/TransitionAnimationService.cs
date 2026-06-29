using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Bootstrap.Loading
{
    // Sprite-only transition cover (no animation yet — that is a later task).
    // Lives on the UIManagerCanvas prefab (DontDestroyOnLoad), so a single instance survives the scene
    // swap: cover is raised in the boot flow and revealed once GameplayScene is data-ready.
    // Cover (= _cover GameObject) is a full-screen Image on its own high-sortingOrder Canvas, disabled by default.
    public sealed class TransitionAnimationService : MonoBehaviour, ITransitionAnimationService
    {
        private const string LogPrefix = "[Transition]";

        [SerializeField] private GameObject _cover;

        public UniTask PlayCoverAsync(CancellationToken ct)
        {
            SetCoverActive(true);
            return UniTask.CompletedTask;
        }

        public UniTask PlayRevealAsync(CancellationToken ct)
        {
            SetCoverActive(false);
            return UniTask.CompletedTask;
        }

        private void SetCoverActive(bool active)
        {
            if (_cover == null)
            {
                Debug.LogWarning($"{LogPrefix} _cover is not assigned on the UIManagerCanvas prefab — skipping.");
                return;
            }

            _cover.SetActive(active);
        }
    }
}
