using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Analytics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
            // The composite service logs an error when a provider throws and then continues
            // with the remaining providers. Expect that log so the test runner doesn't fail on it.
            LogAssert.Expect(LogType.Error, new Regex(@"\[Analytics\].*Provider 'firebase' TrackEvent failed"));

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

        [Test]
        public void Initialize_ConsentDenied_DoesNotInitializeProviders()
        {
            var provider = new RecordingProvider("debug");
            var service = CreateService(provider, consentService: new DeniedConsentService());

            service.Initialize();

            Assert.That(service.IsInitialized, Is.False);
            Assert.That(provider.IsInitialized, Is.False);
        }

        [Test]
        public void SetUserId_ConsentDenied_DoesNotForwardToProviders()
        {
            var provider = new RecordingProvider("debug");
            var service = CreateService(provider, consentService: new DeniedConsentService());

            service.SetUserId("user-1");

            Assert.That(provider.UserId, Is.Null);
        }

        [Test]
        public void TrackEvent_ConsentDenied_DropsWithoutQueueing()
        {
            var provider = new RecordingProvider("debug");
            var queue = new AnalyticsQueue(new TestAnalyticsConfig());
            var service = CreateService(provider, queue: queue, consentService: new DeniedConsentService());

            service.TrackEvent(new AnalyticsEvent("app_started"));

            Assert.That(queue.Count, Is.EqualTo(0));
            Assert.That(provider.Events, Is.Empty);
        }

        [Test]
        public void TrackEvent_NoEnabledProviders_WarnsOnlyOnce()
        {
            // A release Standalone build genuinely has zero providers: debug logging is off by build
            // type and FirebaseAnalyticsProvider is only registered for Android/iOS. Warning per event
            // there would flood the log, which is exactly what REL-12 set out to stop.
            //
            // Counting through logMessageReceived rather than LogAssert on purpose: Unity does not
            // treat surplus warnings as failures, so LogAssert would pass even if the warning fired
            // on every event.
            var warnings = 0;

            void CountWarnings(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Warning && condition.Contains("No enabled analytics providers"))
                {
                    warnings++;
                }
            }

            Application.logMessageReceived += CountWarnings;
            try
            {
                var service = CreateService(Array.Empty<IAnalyticsProvider>(), null, null);
                service.Initialize();

                service.TrackEvent(new AnalyticsEvent("app_started"));
                service.TrackEvent(new AnalyticsEvent("day_started"));
                service.TrackEvent(new AnalyticsEvent("day_completed"));
            }
            finally
            {
                Application.logMessageReceived -= CountWarnings;
            }

            Assert.That(warnings, Is.EqualTo(1), "The warning must be logged once, not per event.");
        }

        private static CompositeAnalyticsService CreateService(
            params IAnalyticsProvider[] providers)
        {
            return CreateService(providers, null, null);
        }

        private static CompositeAnalyticsService CreateService(
            IAnalyticsProvider provider,
            IAnalyticsQueue queue)
        {
            return CreateService(new[] { provider }, queue, null);
        }

        private static CompositeAnalyticsService CreateService(
            IAnalyticsProvider provider,
            IAnalyticsQueue queue,
            IAnalyticsConsentService consentService)
        {
            return CreateService(new[] { provider }, queue, consentService);
        }

        private static CompositeAnalyticsService CreateService(
            IAnalyticsProvider provider,
            IAnalyticsConsentService consentService)
        {
            return CreateService(new[] { provider }, null, consentService);
        }

        private static CompositeAnalyticsService CreateService(
            IAnalyticsProvider[] providers,
            IAnalyticsQueue queue,
            IAnalyticsConsentService consentService = null)
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
                consentService ?? new StubAnalyticsConsentService(),
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

    public sealed class DeniedConsentService : IAnalyticsConsentService
    {
        public bool CanSendAnalytics => false;
        public bool CanSendAttributionData => false;
        public bool CanSendPersonalizedAdsData => false;
        public void SetAnalyticsConsent(bool isAllowed) { }
        public void SetAttributionConsent(bool isAllowed) { }
        public void SetPersonalizedAdsConsent(bool isAllowed) { }
    }
}
