using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI;
using UnityEngine;

namespace Game.Ftue
{
    /// <summary>
    /// <see cref="WindowAnimation"/> driven by an <see cref="Animator"/>: the show/hide animations are
    /// the "Appear"/"Disappear" states (state name must match the trigger name). Hide can be held open
    /// past its clip via <see cref="BlockHideAnimation"/> — e.g. while an FTUE step finishes.
    /// </summary>
    public class AnimatorWindowAnimation : WindowAnimation
    {
        private const string AppearTrigger = "Appear";
        private const string DisappearTrigger = "Disappear";
        private const int Layer = 0;

        [SerializeField] protected Animator _animator;
        [SerializeField, Min(0f)] private float _defaultDuration = 0.25f;

        private int _hideBlockers;

        public override float DefaultDuration => _defaultDuration;

        public IDisposable BlockHideAnimation()
        {
            _hideBlockers++;
            return new HideBlock(this);
        }

        public override UniTask PlayInAsync(CancellationToken ct)
            => PlayAsync(AppearTrigger, ct);

        public override async UniTask PlayOutAsync(CancellationToken ct)
        {
            await PlayAsync(DisappearTrigger, ct);

            // Keep the window in its hidden-end state until every blocker is released.
            await UniTask.WaitWhile(() => _hideBlockers > 0, cancellationToken: ct);
        }

        private async UniTask PlayAsync(string trigger, CancellationToken ct)
        {
            if (_animator == null)
            {
                Debug.LogError($"[{nameof(AnimatorWindowAnimation)}] Animator is not assigned on '{name}'.");
                return;
            }

            _animator.speed = 1f;
            _animator.SetTrigger(trigger);

            // SetTrigger only takes effect on the next evaluation, and a transition may precede the
            // target state — wait until we are actually inside it before measuring its progress.
            await UniTask.WaitUntil(
                () => _animator.GetCurrentAnimatorStateInfo(Layer).IsName(trigger)
                      && !_animator.IsInTransition(Layer),
                cancellationToken: ct);

            // Then wait for the (non-looping) state to play through once.
            await UniTask.WaitUntil(
                () => _animator.GetCurrentAnimatorStateInfo(Layer).normalizedTime >= 1f,
                cancellationToken: ct);
        }

        private void ReleaseHideBlock()
        {
            if (_hideBlockers > 0) _hideBlockers--;
        }

        private sealed class HideBlock : IDisposable
        {
            private AnimatorWindowAnimation _owner;

            public HideBlock(AnimatorWindowAnimation owner) => _owner = owner;

            public void Dispose()
            {
                _owner?.ReleaseHideBlock();
                _owner = null;   // idempotent: a second Dispose is a no-op.
            }
        }
    }
}
