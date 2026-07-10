using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Resources.API;
using MessagePipe;
using NUnit.Framework;
using UIShared;
using UnityEngine;

namespace Game.Core.UI.Tests.Editor.ResourceCounters
{
    public sealed class ResourceCounterHudPresenterTests
    {
        private readonly List<GameObject> _objects = new();

        [SetUp]
        public void SetUp()
        {
            ResourceCounterTargets.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            ResourceCounterTargets.Clear();
            foreach (var obj in _objects)
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            _objects.Clear();
        }

        [Test]
        public void TargetRegistered_UsesCurrentBalance_WhenNoDisplayedCacheExists()
        {
            var resources = new FakeResourcesService();
            resources.Set("Gold", 100);
            var registry = new ResourceCounterTargetRegistry();
            using var presenter = StartPresenter(resources, registry);
            var target = CreateTarget("Gold");

            registry.Register(target);

            Assert.AreEqual(100, target.DisplayedAmount);
        }

        [Test]
        public void NormalResourceChange_UpdatesTargetImmediately()
        {
            var resources = new FakeResourcesService();
            var registry = new ResourceCounterTargetRegistry();
            using var presenter = StartPresenter(resources, registry);
            var target = CreateTarget("Gold");
            registry.Register(target);

            resources.Add("Gold", 25, "shop");

            Assert.AreEqual(25, target.DisplayedAmount);
        }

        [Test]
        public void SalesDayChange_SuppressesImmediateUpdate_ThenCountUpReachesActualBalance()
        {
            var resources = new FakeResourcesService();
            resources.Set("Gold", 100);
            var registry = new ResourceCounterTargetRegistry();
            var subscriber = new FakeCountUpSubscriber();
            using var presenter = StartPresenter(resources, registry, subscriber);
            var target = CreateTarget("Gold");
            registry.Register(target);

            resources.Add("Gold", 50, "sales_day_3_active_book");
            Assert.AreEqual(100, target.DisplayedAmount);

            subscriber.Publish(new ResourceCounterCountUpRequested("Gold"));

            Assert.AreEqual(150, target.DisplayedAmount);
            Assert.AreEqual(1, target.ArriveFeedbackCount);
        }

        [Test]
        public void LateTargetRegistration_AfterSalesDayChange_UsesSuppressedDisplayedCache()
        {
            var resources = new FakeResourcesService();
            resources.Set("Gold", 100);
            var registry = new ResourceCounterTargetRegistry();
            using var presenter = StartPresenter(resources, registry);

            resources.Add("Gold", 50, "sales_day_3_active_book");
            var target = CreateTarget("Gold");
            registry.Register(target);

            Assert.AreEqual(100, target.DisplayedAmount);
        }

        [Test]
        public void RepeatedCountUpRequests_WhileInProgress_DriveRampOnce()
        {
            var resources = new FakeResourcesService();
            resources.Set("Gold", 100);
            var registry = new ResourceCounterTargetRegistry();
            var subscriber = new FakeCountUpSubscriber();
            using var presenter = StartPresenter(resources, registry, subscriber);
            var target = CreateTarget("Gold");
            registry.Register(target);

            resources.Add("Gold", 50, "sales_day_3_active_book");
            target.HoldNextAnimation();

            // Three landing coins publish the same request while the ramp is still in flight.
            subscriber.Publish(new ResourceCounterCountUpRequested("Gold"));
            subscriber.Publish(new ResourceCounterCountUpRequested("Gold"));
            subscriber.Publish(new ResourceCounterCountUpRequested("Gold"));

            Assert.AreEqual(1, target.AnimateCallCount, "Only the first request should drive the ramp.");

            // Finishing the ramp clears the guard, so the next pack runs again.
            target.CompleteAnimation();
            Assert.AreEqual(1, target.ArriveFeedbackCount);

            resources.Add("Gold", 10, "sales_day_4_active_book");
            subscriber.Publish(new ResourceCounterCountUpRequested("Gold"));

            Assert.AreEqual(2, target.AnimateCallCount, "A new pack after completion should run again.");
            Assert.AreEqual(160, target.DisplayedAmount);
        }

        private ResourceCounterHudPresenter StartPresenter(
            FakeResourcesService resources,
            IResourceCounterTargetRegistry registry,
            ISubscriber<ResourceCounterCountUpRequested> subscriber = null)
        {
            var presenter = new ResourceCounterHudPresenter(resources, registry, subscriber);
            presenter.Start();
            return presenter;
        }

        private FakeCounterTarget CreateTarget(string resourceId)
        {
            var go = new GameObject(resourceId, typeof(RectTransform));
            _objects.Add(go);
            return new FakeCounterTarget(resourceId, (RectTransform)go.transform);
        }
    }

    internal sealed class FakeCounterTarget : IResourceCounterTarget
    {
        public FakeCounterTarget(string resourceId, RectTransform rectTransform)
        {
            ResourceId = resourceId;
            RectTransform = rectTransform;
        }

        private UniTaskCompletionSource _pending;

        public string ResourceId { get; }
        public RectTransform RectTransform { get; }
        public int DisplayedAmount { get; private set; }
        public int ArriveFeedbackCount { get; private set; }
        public int AnimateCallCount { get; private set; }

        // Keeps the next AnimateAmountToAsync pending so a test can fire more requests while a
        // count-up is "in flight", then release it with CompleteAnimation.
        public void HoldNextAnimation() => _pending = new UniTaskCompletionSource();

        public void CompleteAnimation()
        {
            var pending = _pending;
            _pending = null;
            pending?.TrySetResult();
        }

        public void SetAmountImmediate(int amount)
        {
            DisplayedAmount = Math.Max(0, amount);
        }

        public UniTask AnimateAmountToAsync(int amount, CancellationToken ct = default)
        {
            AnimateCallCount++;
            DisplayedAmount = Math.Max(0, amount);
            return _pending != null ? _pending.Task : UniTask.CompletedTask;
        }

        public void PlayArriveFeedback()
        {
            ArriveFeedbackCount++;
        }
    }

    internal sealed class FakeResourcesService : IResourcesService
    {
        private readonly Dictionary<string, int> _amounts = new(StringComparer.Ordinal);

        public event Action<ResourceChangeEvent> Changed;

        public IReadOnlyDictionary<string, int> GetAll() => _amounts;

        public int GetAmount(string resourceId)
            => !string.IsNullOrEmpty(resourceId) && _amounts.TryGetValue(resourceId, out var amount) ? amount : 0;

        public bool Has(string resourceId, int amount) => GetAmount(resourceId) >= amount;

        public UniTask AddAsync(string resourceId, int amount, string reason, CancellationToken ct)
        {
            Add(resourceId, amount, reason);
            return UniTask.CompletedTask;
        }

        public UniTask<bool> RemoveAsync(string resourceId, int amount, string reason, CancellationToken ct)
        {
            var old = GetAmount(resourceId);
            var next = Math.Max(0, old - amount);
            _amounts[resourceId] = next;
            Changed?.Invoke(new ResourceChangeEvent(resourceId, old, next, next - old, reason));
            return UniTask.FromResult(true);
        }

        public void Set(string resourceId, int amount)
        {
            _amounts[resourceId] = Math.Max(0, amount);
        }

        public void Add(string resourceId, int amount, string reason)
        {
            var old = GetAmount(resourceId);
            var next = old + Math.Max(0, amount);
            _amounts[resourceId] = next;
            Changed?.Invoke(new ResourceChangeEvent(resourceId, old, next, next - old, reason));
        }
    }

    internal sealed class FakeCountUpSubscriber : ISubscriber<ResourceCounterCountUpRequested>
    {
        private IMessageHandler<ResourceCounterCountUpRequested> _handler;

        public IDisposable Subscribe(
            IMessageHandler<ResourceCounterCountUpRequested> handler,
            params MessageHandlerFilter<ResourceCounterCountUpRequested>[] filters)
        {
            _handler = handler;
            return new Subscription(() => _handler = null);
        }

        public void Publish(ResourceCounterCountUpRequested message)
        {
            _handler?.Handle(message);
        }

        private sealed class Subscription : IDisposable
        {
            private readonly Action _dispose;

            public Subscription(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose()
            {
                _dispose?.Invoke();
            }
        }
    }
}
