using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Book.Sell.UI.Customer
{
    public sealed class CustomerVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _figure;
        [SerializeField] private Sprite _fallbackSprite;
        [SerializeField] private Transform _bubbleAnchor;

        public Domain.Customer Customer { get; private set; }
        public Transform BubbleAnchor => _bubbleAnchor != null ? _bubbleAnchor : transform;

        private CancellationTokenSource _moveCts;

        public void Initialize(Domain.Customer customer)
        {
            Customer = customer;
            gameObject.name = $"CustomerVisual({customer.Id})";

            Debug.Log(
                $"[CustomerVisual] Initialize customer='{customer.Id}', characterId='{customer.CharacterId}', " +
                $"figure='{ObjectName(_figure)}', fallback='{ObjectName(_fallbackSprite)}', currentSprite='{ObjectName(CurrentFigureSprite())}'.");
        }

        public void ApplyFigureSprite(Sprite sprite)
        {
            if (_figure == null)
            {
                Debug.LogWarning(
                    $"[CustomerVisual] Cannot apply NPC sprite for {DescribeCustomer()}: _figure is not assigned. " +
                    $"requestedSprite='{ObjectName(sprite)}', fallback='{ObjectName(_fallbackSprite)}'.");
                return;
            }

            if (sprite == null)
            {
                _figure.sprite = _fallbackSprite;
                Debug.LogWarning(
                    $"[CustomerVisual] NPC sprite is null for {DescribeCustomer()}; applied fallback='{ObjectName(_fallbackSprite)}'. " +
                    $"figure='{ObjectName(_figure)}', resultSprite='{ObjectName(_figure.sprite)}'.");
                return;
            }

            _figure.sprite = sprite;
            Debug.Log(
                $"[CustomerVisual] Applied NPC sprite for {DescribeCustomer()}: sprite='{ObjectName(sprite)}', " +
                $"figure='{ObjectName(_figure)}', resultSprite='{ObjectName(_figure.sprite)}'.");
        }

        public string DescribeFigureState()
        {
            return $"figure='{ObjectName(_figure)}', currentSprite='{ObjectName(CurrentFigureSprite())}', fallback='{ObjectName(_fallbackSprite)}'";
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

        private string DescribeCustomer()
        {
            return Customer == null
                ? "customer='<not initialized>'"
                : $"customer='{Customer.Id}', characterId='{Customer.CharacterId}'";
        }

        private static string ObjectName(UnityEngine.Object target)
        {
            return target != null ? target.name : "<null>";
        }

        private Sprite CurrentFigureSprite()
        {
            return _figure != null ? _figure.sprite : null;
        }
    }
}
