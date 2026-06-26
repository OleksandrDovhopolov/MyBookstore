using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI;
using UnityEngine;

namespace Game.Ftue
{
    /// <summary>
    /// <see cref="WindowAnimation"/> for the welcome letter, driven by an <see cref="Animator"/>.
    /// Distinct from <see cref="AnimatorWindowAnimation"/> because it has an extra beat: after the
    /// show clip it settles into a looping idle, and the controller later asks it to play the
    /// one-shot "letter click" clip (envelope opening) before the Start button becomes available.
    ///
    /// Animator Controller contract (state name == trigger name):
    /// <list type="bullet">
    /// <item><c>Appear</c> — ShowAnimation; auto-transitions into the looping <c>LetterIdle</c>.</item>
    /// <item><c>LetterClick</c> — one-shot envelope-open clip.</item>
    /// <item><c>Disappear</c> — HideAnimation.</item>
    /// </list>
    /// </summary>
    public class WelcomeWindowAnimation : WindowAnimation
    {
        private const string AppearTrigger = "Appear";
        private const string LetterClickTrigger = "LetterClick";
        private const string DisappearTrigger = "Disappear";
        private const int Layer = 0;
        private const float EnterStateTimeoutSeconds = 1f;

        [SerializeField] protected Animator _animator;
        [SerializeField, Min(0f)] private float _defaultDuration = 0.25f;

        public override float DefaultDuration => _defaultDuration;

        /// <summary>
        /// Plays the show clip and returns once it finishes. Does NOT wait on the looping idle that
        /// follows (it never completes) — the Animator auto-transitions into <c>LetterIdle</c>.
        /// </summary>
        public override UniTask PlayInAsync(CancellationToken ct) => PlayClipAsync(AppearTrigger, ct);

        /// <summary>Plays the one-shot envelope-open clip and waits for it to finish (step 5).</summary>
        public UniTask PlayLetterClickAsync(CancellationToken ct) => PlayClipAsync(LetterClickTrigger, ct);

        public override UniTask PlayOutAsync(CancellationToken ct) => PlayClipAsync(DisappearTrigger, ct);

        private async UniTask PlayClipAsync(string trigger, CancellationToken ct)
        {
            if (_animator == null)
            {
                Debug.LogError($"[{nameof(WelcomeWindowAnimation)}] Animator is not assigned on '{name}'.");
                return;
            }

            _animator.speed = 1f;
            _animator.SetTrigger(trigger);

            // SetTrigger takes effect next evaluation and a transition may precede the target state —
            // wait until we are actually inside it before measuring progress. Bounded by a timeout so a
            // missing state / unreachable transition can't hang the caller forever (PlayOutAsync runs
            // inside the UIManager gate — an infinite wait would deadlock all window open/close).
            var enterDeadline = Time.unscaledTime + EnterStateTimeoutSeconds;
            await UniTask.WaitUntil(
                () => (_animator.GetCurrentAnimatorStateInfo(Layer).IsName(trigger) && !_animator.IsInTransition(Layer))
                      || Time.unscaledTime >= enterDeadline,
                cancellationToken: ct);

            if (!_animator.GetCurrentAnimatorStateInfo(Layer).IsName(trigger))
            {
                Debug.LogWarning($"[{nameof(WelcomeWindowAnimation)}] state '{trigger}' was not entered within " +
                                 $"{EnterStateTimeoutSeconds}s — add a '{trigger}' state with a reachable " +
                                 "(Any State) transition. Continuing without waiting so the window is not stuck.");
                return;
            }

            // Then wait for the (non-looping) clip to play through once.
            await UniTask.WaitUntil(
                () => _animator.GetCurrentAnimatorStateInfo(Layer).normalizedTime >= 1f,
                cancellationToken: ct);
        }
    }
}
