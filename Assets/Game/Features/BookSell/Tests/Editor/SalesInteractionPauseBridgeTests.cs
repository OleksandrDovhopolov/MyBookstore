using System;
using System.Collections.Generic;
using Book.Sell.Services;
using Game.UI;
using MessagePipe;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor
{
    public sealed class SalesInteractionPauseBridgeTests
    {
        [Test]
        public void PausedTrue_AcquiresInteractionLock()
        {
            var h = new Harness();
            h.Bridge.Start();

            h.Subscriber.Publish(new SalesPauseRequested(true));

            Assert.IsTrue(h.Lock.IsHeld);
        }

        [Test]
        public void PausedFalse_ReleasesHolder()
        {
            var h = new Harness();
            h.Bridge.Start();
            h.Subscriber.Publish(new SalesPauseRequested(true));

            h.Subscriber.Publish(new SalesPauseRequested(false));

            Assert.IsFalse(h.Lock.IsHeld);
        }

        [Test]
        public void PausedFalse_RemovesQueuedWaiter_WhenLockWasHeldByOther()
        {
            var h = new Harness();
            var other = new object();
            h.Lock.TryAcquire(other);
            h.Bridge.Start();

            h.Subscriber.Publish(new SalesPauseRequested(true));
            h.Subscriber.Publish(new SalesPauseRequested(false));
            h.Lock.Release(other);

            Assert.IsTrue(h.Lock.TryAcquire(new object()), "Pause token must not remain as a ghost waiter.");
        }

        [Test]
        public void Dispose_Unsubscribes_AndReleases()
        {
            var h = new Harness();
            h.Bridge.Start();
            h.Subscriber.Publish(new SalesPauseRequested(true));

            h.Bridge.Dispose();
            h.Subscriber.Publish(new SalesPauseRequested(true));

            Assert.IsFalse(h.Lock.IsHeld);
        }

        private sealed class Harness
        {
            public InteractionLock Lock { get; } = new();
            public RecordingSubscriber<SalesPauseRequested> Subscriber { get; } = new();
            public SalesInteractionPauseBridge Bridge { get; }

            public Harness()
            {
                Bridge = new SalesInteractionPauseBridge(Lock, Subscriber);
            }
        }

        private sealed class RecordingSubscriber<T> : ISubscriber<T>
        {
            private readonly List<IMessageHandler<T>> _handlers = new();

            public IDisposable Subscribe(
                IMessageHandler<T> handler,
                params MessageHandlerFilter<T>[] filters)
            {
                _handlers.Add(handler);
                return new Subscription(this, handler);
            }

            public void Publish(T message)
            {
                for (var i = 0; i < _handlers.Count; i++)
                    _handlers[i]?.Handle(message);
            }

            private sealed class Subscription : IDisposable
            {
                private readonly RecordingSubscriber<T> _owner;
                private readonly IMessageHandler<T> _handler;
                private bool _disposed;

                public Subscription(RecordingSubscriber<T> owner, IMessageHandler<T> handler)
                {
                    _owner = owner;
                    _handler = handler;
                }

                public void Dispose()
                {
                    if (_disposed) return;
                    _disposed = true;
                    _owner._handlers.Remove(_handler);
                }
            }
        }
    }
}
