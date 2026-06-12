using System;
using Analytics;
using System.Collections.Generic;
using NUnit.Framework;

namespace AnalyticsTests.Editor
{
    public sealed class CompositeAnalyticsServiceTests
    {
        [Test]
        public void TrackEvent_MergesCommonParameters_WithCustomPriority()
        {
            var provider = new RecordingProvider("debug");
            var service = CreateService(provider);
            service.Initialize();

            service.TrackEvent(new AnalyticsEvent("level_completed", new Dictionary<string, object>
            {
                [AnalyticsParameterNames.PlayerLevel] = 12
            }));

            Assert.That(provider.Events, Has.Count.EqualTo(1));
            Assert.That(provider.Events[0].Parameters[AnalyticsParameterNames.PlayerLevel], Is.EqualTo(12));
            Assert.That(provider.Events[0].Parameters[AnalyticsParameterNames.SessionId], Is.EqualTo("session"));
        }

        [Test]
        public void TrackEvent_QueuesBeforeInitialize_AndFlushesAfterInitialize()
        {
            var provider = new RecordingProvider("debug");
            var queue = new AnalyticsQueue(new TestAnalyticsConfig());
            var service = CreateService(provider, queue: queue);

            service.TrackEvent(new AnalyticsEvent("app_started"));

            Assert.That(queue.Count, Is.EqualTo(1));

            service.Initialize();

            Assert.That(queue.Count, Is.EqualTo(0));
            Assert.That(provider.Events, Has.Count.EqualTo(1));
        }

        [Test]
        public void TrackEvent_ContinuesWhenProviderThrows()
        {
            var throwingProvider = new ThrowingProvider("firebase");
            var recordingProvider = new RecordingProvider("debug");
            var service = CreateService(throwingProvider, recordingProvider);
            service.Initialize();

            service.TrackEvent(new AnalyticsEvent("app_started"));

            Assert.That(recordingProvider.Events, Has.Count.EqualTo(1));
        }

        [Test]
        public void SetUserId_ForwardsToProviders()
        {
            var provider = new RecordingProvider("debug");
            var service = CreateService(provider);
            service.Initialize();

            service.SetUserId("user-1");

            Assert.That(provider.UserId, Is.EqualTo("user-1"));
        }

        private static CompositeAnalyticsService CreateService(
            params IAnalyticsProvider[] providers)
        {
            return CreateService(providers, null);
        }

        private static CompositeAnalyticsService CreateService(
            IAnalyticsProvider provider,
            IAnalyticsQueue queue)
        {
            return CreateService(new[] { provider }, queue);
        }

        private static CompositeAnalyticsService CreateService(
            IAnalyticsProvider[] providers,
            IAnalyticsQueue queue)
        {
            var config = new TestAnalyticsConfig
            {
                EnabledProviderIdsValue = new[] { "debug", "firebase" }
            };
            return new CompositeAnalyticsService(
                config,
                new TestContextProvider(),
                new DefaultAnalyticsEventValidator(config),
                new DefaultAnalyticsRouter(new DefaultAnalyticsRoutingConfig()),
                new DefaultAnalyticsEventMapper(new DefaultAnalyticsMappingConfig()),
                queue ?? new AnalyticsQueue(config),
                new StubAnalyticsConsentService(),
                providers);
        }
    }

    public class RecordingProvider : IAnalyticsProvider
    {
        public RecordingProvider(string providerId)
        {
            ProviderId = providerId;
        }

        public string ProviderId { get; }

        public bool IsEnabled => true;

        public bool IsInitialized { get; private set; }

        public List<IAnalyticsEvent> Events { get; } = new();

        public string UserId { get; private set; }

        public void Initialize()
        {
            IsInitialized = true;
        }

        public virtual void TrackEvent(IAnalyticsEvent analyticsEvent)
        {
            Events.Add(analyticsEvent);
        }

        public void SetUserId(string userId)
        {
            UserId = userId;
        }

        public void SetUserProperty(string key, string value)
        {
        }

        public void Flush()
        {
        }
    }

    public sealed class ThrowingProvider : RecordingProvider
    {
        public ThrowingProvider(string providerId)
            : base(providerId)
        {
        }

        public override void TrackEvent(IAnalyticsEvent analyticsEvent)
        {
            throw new InvalidOperationException("Provider failed.");
        }
    }

    public sealed class TestContextProvider : IAnalyticsContextProvider
    {
        public IReadOnlyDictionary<string, object> GetCommonParameters()
        {
            return new Dictionary<string, object>
            {
                [AnalyticsParameterNames.SessionId] = "session",
                [AnalyticsParameterNames.PlayerLevel] = 10,
                [AnalyticsParameterNames.UserId] = "install-user"
            };
        }
    }
}
