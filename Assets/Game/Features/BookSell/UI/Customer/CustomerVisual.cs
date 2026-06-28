using Book.Sell.Domain;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

namespace Book.Sell.UI.Customer
{
    public sealed class CustomerVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _figure;
        [SerializeField] private Transform _bubbleAnchor;

        public Book.Sell.Domain.Customer Customer { get; private set; }
        public Transform BubbleAnchor => _bubbleAnchor != null ? _bubbleAnchor : transform;

        private CancellationTokenSource _moveCts;

        public void Initialize(Book.Sell.Domain.Customer customer)
        {
            Customer = customer;
            gameObject.name = $"CustomerVisual({customer.Id})";
        }

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
