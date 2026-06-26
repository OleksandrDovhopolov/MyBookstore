using System;
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
    /// Each beat fires its trigger and then waits a serialized duration (set to the clip length in the
    /// inspector). We do NOT poll <c>Animator</c> state by name — that is fragile (state names must
    /// equal trigger names) and silently times out when they don't, which delays hide/reveal.
    /// </summary>
    public class WelcomeWindowAnimation : WindowAnimation
    {
        private const string AppearTrigger = "Appear";
        private const string LetterClickTrigger = "Click";
        private const string DisappearTrigger = "Disappear";

        [SerializeField] protected Animator _animator;

        [Header("Durations (match the clip lengths)")]
        [SerializeField, Min(0f)] private float _appearDuration = 0.5f;
        [SerializeField, Min(0f)] private float _letterClickDuration = 0.5f;
        [SerializeField, Min(0f)] private float _disappearDuration = 0.35f;

        public override float DefaultDuration => _disappearDuration;

        /// <summary>
        /// Plays the show clip. After <see cref="_appearDuration"/> the Animator is expected to be in
        /// the looping <c>LetterIdle</c> — we don't wait on that loop.
        /// </summary>
        public override UniTask PlayInAsync(CancellationToken ct) => PlayClipAsync(AppearTrigger, _appearDuration, ct);

        /// <summary>Plays the one-shot envelope-open clip and waits for it to finish (step 5).</summary>
        public UniTask PlayLetterClickAsync(CancellationToken ct) => PlayClipAsync(LetterClickTrigger, _letterClickDuration, ct);

        public override UniTask PlayOutAsync(CancellationToken ct) => PlayClipAsync(DisappearTrigger, _disappearDuration, ct);

        private async UniTask PlayClipAsync(string trigger, float duration, CancellationToken ct)
        {
            if (_animator == null)
            {
                Debug.LogError($"[{nameof(WelcomeWindowAnimation)}] Animator is not assigned on '{name}'.");
                return;
            }

            _animator.speed = 1f;
            _animator.SetTrigger(trigger);

            if (duration > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(duration), DelayType.UnscaledDeltaTime, cancellationToken: ct);
        }
    }
}
