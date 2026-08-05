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
        private readonly ISubscriber<ResourceCounterDisplayOverrideChanged> _overrideSubscriber;
        private readonly Dictionary<string, int> _displayedAmounts = new(StringComparer.Ordinal);
        private readonly HashSet<string> _displayOverrides = new(StringComparer.Ordinal);
        private readonly HashSet<string> _countUpInProgress = new(StringComparer.Ordinal);

        private IDisposable _countUpSubscription;
        private IDisposable _overrideSubscription;
        private bool _started;

        public ResourceCounterHudPresenter(
            IResourcesService resources,
            IResourceCounterTargetRegistry targets,
            ISubscriber<ResourceCounterCountUpRequested> countUpSubscriber = null,
            ISubscriber<ResourceCounterDisplayOverrideChanged> overrideSubscriber = null)
        {
            _resources = resources;
            _targets = targets;
            _countUpSubscriber = countUpSubscriber;
            _overrideSubscriber = overrideSubscriber;
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

            if (_overrideSubscriber != null)
                _overrideSubscription = _overrideSubscriber.Subscribe(new DisplayOverrideHandler(this));
        }

        public void Dispose()
        {
            if (!_started) return;
            _started = false;

            _countUpSubscription?.Dispose();
            _countUpSubscription = null;

            _overrideSubscription?.Dispose();
            _overrideSubscription = null;

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

            if (_displayOverrides.Contains(change.ResourceId))
                return;

            if (IsSalesDayChange(change))
            {
                if (!_displayedAmounts.ContainsKey(change.ResourceId))
                    _displayedAmounts[change.ResourceId] = Math.Max(0, change.OldAmount);
                return;
            }

            SetDisplayedAmount(change.ResourceId, change.NewAmount, animate: true);
        }

        private void OnDisplayOverrideChanged(ResourceCounterDisplayOverrideChanged change)
        {
            if (string.IsNullOrWhiteSpace(change.ResourceId)) return;

            if (change.Active)
            {
                _displayOverrides.Add(change.ResourceId);
                SetDisplayedAmount(change.ResourceId, change.Amount);
                return;
            }

            _displayOverrides.Remove(change.ResourceId);
            SetDisplayedAmount(change.ResourceId, _resources?.GetAmount(change.ResourceId) ?? 0);
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
            if (_displayOverrides.Contains(resourceId)) return;

            // Count-up is triggered per landing coin, so the same request arrives several times
            // for one pack. Only the first drives the ramp; the rest are ignored until it finishes,
            // otherwise the SetAmountImmediate below would yank the counter back to the old value on
            // every coin. The guard clears in finally, so the next day's pack runs again.
            if (!_countUpInProgress.Add(resourceId)) return;

            var finalAmount = Math.Max(0, _resources?.GetAmount(resourceId) ?? 0);

            if (_targets == null || !_targets.TryGetTarget(resourceId, out var target) || target == null)
            {
                _displayedAmounts[resourceId] = finalAmount;
                _countUpInProgress.Remove(resourceId);
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
                // Only the active target ran the ramp; the rest jump straight to the final amount.
                ApplyToAllTargets(resourceId, finalAmount, animate: false);
                _countUpInProgress.Remove(resourceId);
            }
        }

        private void SetDisplayedAmount(string resourceId, int amount, bool animate = false)
        {
            if (string.IsNullOrWhiteSpace(resourceId)) return;

            var clamped = Math.Max(0, amount);
            _displayedAmounts[resourceId] = clamped;
            ApplyToAllTargets(resourceId, clamped, animate);
        }

        // A window can host its own counter for the same resource (the shop shows the gold balance).
        // Every live target is updated, so the one currently hidden behind a window is correct the
        // moment it becomes visible again instead of showing a stale amount. Only the active target
        // ramps — animating counters nobody can see would just burn frames and could land mid-ramp
        // when the window closes.
        private void ApplyToAllTargets(string resourceId, int amount, bool animate)
        {
            if (_targets == null) return;

            // A day payout is already ramping this resource; interrupting it would look like a glitch.
            if (animate && _countUpInProgress.Contains(resourceId))
                animate = false;

            IResourceCounterTarget active = null;
            if (animate && !_targets.TryGetTarget(resourceId, out active))
                active = null;

            var targets = _targets.GetTargets(resourceId);
            for (var i = targets.Count - 1; i >= 0; i--)
            {
                var target = targets[i];
                if (target == null) continue;

                if (ReferenceEquals(target, active))
                    target.AnimateChangeAsync(amount).Forget();
                else
                    target.SetAmountImmediate(amount);
            }
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
                _presenter.AnimateCountUpInternalAsync(message.ResourceId, CancellationToken.None).Forget();
            }
        }

        private sealed class DisplayOverrideHandler : IMessageHandler<ResourceCounterDisplayOverrideChanged>
        {
            private readonly ResourceCounterHudPresenter _presenter;

            public DisplayOverrideHandler(ResourceCounterHudPresenter presenter)
            {
                _presenter = presenter;
            }

            public void Handle(ResourceCounterDisplayOverrideChanged message)
            {
                _presenter.OnDisplayOverrideChanged(message);
            }
        }
    }
}
