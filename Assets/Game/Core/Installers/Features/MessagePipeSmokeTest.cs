using System;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    public readonly struct MessagePipeSmokeEvent
    {
        public readonly int Payload;
        public MessagePipeSmokeEvent(int payload) => Payload = payload;
    }

    // Temporary smoke test. Remove once the first real message broker is wired up.
    // To enable:
    //   1. Add this file's installer call to BootstrapInstaller.InstallBindings:
    //        builder.RegisterMessagePipeSmokeTest();
    //   2. Press Play. The console should show one [MessagePipe] received line per published event.
    public static class MessagePipeSmokeTestBindings
    {
        public static void RegisterMessagePipeSmokeTest(this IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<MessagePipeSmokeTest>(Lifetime.Singleton);
        }
    }

    internal sealed class MessagePipeSmokeTest : IStartable, IDisposable
    {
        private readonly IPublisher<MessagePipeSmokeEvent> _publisher;
        private readonly ISubscriber<MessagePipeSmokeEvent> _subscriber;
        private IDisposable _subscription;

        public MessagePipeSmokeTest(
            IPublisher<MessagePipeSmokeEvent> publisher,
            ISubscriber<MessagePipeSmokeEvent> subscriber)
        {
            _publisher = publisher;
            _subscriber = subscriber;
        }

        public void Start()
        {
            _subscription = _subscriber.Subscribe(e =>
                Debug.Log($"[MessagePipe] received payload={e.Payload}"));

            _publisher.Publish(new MessagePipeSmokeEvent(1));
            _publisher.Publish(new MessagePipeSmokeEvent(42));
        }

        public void Dispose() => _subscription?.Dispose();
    }
}
