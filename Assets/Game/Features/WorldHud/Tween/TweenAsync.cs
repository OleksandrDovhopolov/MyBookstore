using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.WorldHud
{
    // Small async tween helpers built on UniTask + Mathf.Lerp.
    // Mirrors the loop pattern from Game.UI.FadeAnimation. No DOTween in the project (yet).
    // All overloads honor the CancellationToken — caller is responsible for cancelling
    // overlapping tweens (e.g. when state changes mid-fade).
    public static class TweenAsync
    {
        public static async UniTask LerpAsync(
            float from,
            float to,
            float duration,
            Action<float> apply,
            CancellationToken ct)
        {
            if (apply == null) throw new ArgumentNullException(nameof(apply));

            if (duration <= 0f)
            {
                apply(to);
                return;
            }

            apply(from);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                apply(Mathf.Lerp(from, to, t));
                await UniTask.NextFrame(ct);
            }
            apply(to);
        }

        public static UniTask LerpAlphaAsync(
            CanvasGroup canvasGroup,
            float from,
            float to,
            float duration,
            CancellationToken ct)
        {
            if (canvasGroup == null) return UniTask.CompletedTask;
            // Per-frame null-check: the target can be destroyed mid-fade (e.g. the customer visual the
            // bubble is parented to despawns while it is detaching). Skip the set instead of throwing
            // MissingReferenceException.
            return LerpAsync(from, to, duration, value =>
            {
                if (canvasGroup != null) canvasGroup.alpha = value;
            }, ct);
        }

        public static UniTask LerpScaleAsync(
            Transform target,
            Vector3 from,
            Vector3 to,
            float duration,
            CancellationToken ct)
        {
            if (target == null) return UniTask.CompletedTask;
            return LerpAsync(0f, 1f, duration, t =>
            {
                if (target != null) target.localScale = Vector3.LerpUnclamped(from, to, t);
            }, ct);
        }
    }
}
