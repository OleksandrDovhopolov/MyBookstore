using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Resources.API;
using MessagePipe;

namespace UIShared
{
    public sealed class ResourceCounterHudPresenter : IDisposable
    {
        private const string SalesDayReasonPrefix = "sales_day_";

        private readonly IResourcesService _resources;
        private readonly IResourceCounterTargetRegistry _targets;
        private readonly ISubscriber<ResourceCounterCountUpRequested> _countUpSubscriber;
        private readonly Dictionary<string, int> _displayedAmounts = new(StringComparer.Ordinal);

        private IDisposable _countUpSubscription;
        private bool _started;

        public ResourceCounterHudPresenter(
            IResourcesService resources,
            IResourceCounterTargetRegistry targets,
            ISubscriber<ResourceCounterCountUpRequested> countUpSubscriber = null)
        {
            _resources = resources;
            _targets = targets;
            _countUpSubscriber = countUpSubscriber;
        }

        public void Start()
        {
            if (_started) return;
            _started = true;

            if (_resources != null)
                _resources.Changed += OnResourceChanged;

            if (_targets != null)
                _targets.TargetRegistered += OnTargetRegistered;

            if (_countUpSubscriber != null)
                _countUpSubscription = _countUpSubscriber.Subscribe(new CountUpHandler(this));
        }

        public void Dispose()
        {
            if (!_started) return;
            _started = false;

            _countUpSubscription?.Dispose();
            _countUpSubscription = null;

            if (_targets != null)
                _targets.TargetRegistered -= OnTargetRegistered;

            if (_resources != null)
                _resources.Changed -= OnResourceChanged;
        }

        public UniTask AnimateCountUpAsync(
            string resourceId,
            CancellationToken ct = default)
        {
            return AnimateCountUpInternalAsync(resourceId, ct);
        }

        private void OnResourceChanged(ResourceChangeEvent change)
        {
            if (change == null || string.IsNullOrWhiteSpace(change.ResourceId)) return;

            if (IsSalesDayChange(change))
            {
                if (!_displayedAmounts.ContainsKey(change.ResourceId))
                    _displayedAmounts[change.ResourceId] = Math.Max(0, change.OldAmount);
                return;
            }

            SetDisplayedAmount(change.ResourceId, change.NewAmount);
        }

        private void OnTargetRegistered(IResourceCounterTarget target)
        {
            if (target == null || string.IsNullOrWhiteSpace(target.ResourceId)) return;

            if (!_displayedAmounts.TryGetValue(target.ResourceId, out var amount))
                amount = _resources?.GetAmount(target.ResourceId) ?? 0;

            target.SetAmountImmediate(amount);
            _displayedAmounts[target.ResourceId] = Math.Max(0, amount);
        }

        private async UniTask AnimateCountUpInternalAsync(
            string resourceId,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(resourceId)) return;

            var finalAmount = Math.Max(0, _resources?.GetAmount(resourceId) ?? 0);

            if (_targets == null || !_targets.TryGetTarget(resourceId, out var target) || target == null)
            {
                _displayedAmounts[resourceId] = finalAmount;
                return;
            }

            if (_displayedAmounts.TryGetValue(resourceId, out var displayed))
                target.SetAmountImmediate(displayed);

            try
            {
                await target.AnimateAmountToAsync(finalAmount, ct);
                target.PlayArriveFeedback();
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _displayedAmounts[resourceId] = finalAmount;
            }
        }

        private void SetDisplayedAmount(string resourceId, int amount)
        {
            if (string.IsNullOrWhiteSpace(resourceId)) return;

            var clamped = Math.Max(0, amount);
            _displayedAmounts[resourceId] = clamped;

            if (_targets != null && _targets.TryGetTarget(resourceId, out var target))
                target?.SetAmountImmediate(clamped);
        }

        private static bool IsSalesDayChange(ResourceChangeEvent change)
            => change.Delta > 0
               && !string.IsNullOrEmpty(change.Reason)
               && change.Reason.StartsWith(SalesDayReasonPrefix, StringComparison.Ordinal);

        private sealed class CountUpHandler : IMessageHandler<ResourceCounterCountUpRequested>
        {
            private readonly ResourceCounterHudPresenter _presenter;

            public CountUpHandler(ResourceCounterHudPresenter presenter)
            {
                _presenter = presenter;
            }

            public void Handle(ResourceCounterCountUpRequested message)
            {
                _presenter.AnimateCountUpInternalAsync(
                    message.ResourceId,
                    CancellationToken.None).Forget();
            }
        }
    }
}
