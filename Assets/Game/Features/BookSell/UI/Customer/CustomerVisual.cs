using Book.Sell.Domain;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

namespace Book.Sell.UI.Customer
{
    // Placeholder visualization for a Customer POCO: a simple movable sprite with a child Transform
    // that the world-space bubble can attach to. Real customer art/animation arrives later.
    public sealed class CustomerVisual : MonoBehaviour
    {
        private static readonly Color[] Palette =
        {
            new Color(0.95f, 0.35f, 0.32f),
            new Color(0.25f, 0.62f, 0.95f),
            new Color(0.35f, 0.78f, 0.45f),
            new Color(0.96f, 0.74f, 0.28f),
            new Color(0.68f, 0.45f, 0.92f)
        };

        [SerializeField] private SpriteRenderer _figure;
        [SerializeField] private Transform _bubbleAnchor;

        public Book.Sell.Domain.Customer Customer { get; private set; }
        public Transform BubbleAnchor => _bubbleAnchor != null ? _bubbleAnchor : transform;

        private CancellationTokenSource _moveCts;

        public void Initialize(Book.Sell.Domain.Customer customer)
        {
            Customer = customer;
            gameObject.name = $"CustomerVisual({customer.Id})";
            ApplyCustomerColor(customer);
        }

        private void ApplyCustomerColor(Book.Sell.Domain.Customer customer)
        {
            if (_figure == null || customer == null) return;

            var hash = StableHash(customer.Id);
            _figure.color = Palette[hash % Palette.Length];
        }

        private static int StableHash(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;

            unchecked
            {
                var hash = 23;
                for (var i = 0; i < value.Length; i++)
                    hash = hash * 31 + value[i];
                return hash & int.MaxValue;
            }
        }

        // <paramref name="isPaused"/> lets the caller freeze the walk in lockstep with the paused day
        // (e.g. while the recommendation minigame window is open), so the visual cannot drift ahead of the
        // logical step that is no longer ticking.
        public async UniTask MoveToAsync(Vector3 target, float duration, Func<bool> isPaused = null, CancellationToken ct = default)
        {
            _moveCts?.Cancel();
            var moveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _moveCts = moveCts;

            var token = moveCts.Token;
            var start = transform.position;
            var safeDuration = Mathf.Max(0.01f, duration);
            var elapsed = 0f;

            try
            {
                while (elapsed < safeDuration)
                {
                    token.ThrowIfCancellationRequested();
                    if (isPaused == null || !isPaused())
                    {
                        elapsed += Time.deltaTime;
                        var t = Mathf.Clamp01(elapsed / safeDuration);
                        transform.position = Vector3.Lerp(start, target, t);
                    }
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                transform.position = target;
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (_moveCts == moveCts)
                    _moveCts = null;
                moveCts.Dispose();
            }
        }

        private void OnDestroy()
        {
            _moveCts?.Cancel();
            _moveCts = null;
        }
    }
}
