using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI;
using UnityEngine;

namespace Game.Ftue
{
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

        public override UniTask PlayInAsync(CancellationToken ct) => PlayClipAsync(AppearTrigger, _appearDuration, ct);

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
