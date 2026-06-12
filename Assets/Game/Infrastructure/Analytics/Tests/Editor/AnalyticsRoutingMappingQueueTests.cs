using System.Collections.Generic;
using Analytics;
using System.Linq;
using NUnit.Framework;

namespace AnalyticsTests.Editor
{
    public sealed class AnalyticsRoutingMappingQueueTests
    {
        [Test]
        public void Queue_DropsOldestEvent_WhenOverflowing()
        {
            var queue = new AnalyticsQueue(new TestAnalyticsConfig
            {
                MaxQueueSizeValue = 2
            });

            queue.Enqueue(new AnalyticsEvent("first_event"));
            queue.Enqueue(new AnalyticsEvent("second_event"));
            queue.Enqueue(new AnalyticsEvent("third_event"));

            Assert.That(queue.Count, Is.EqualTo(2));
            Assert.That(queue.TryDequeue(out var first), Is.True);
            Assert.That(first.Name, Is.EqualTo("second_event"));
            Assert.That(queue.TryDequeue(out var second), Is.True);
            Assert.That(second.Name, Is.EqualTo("third_event"));
        }

        [Test]
        public void Router_UsesExplicitRouting_WhenRuleExists()
        {
            var router = new DefaultAnalyticsRouter(new TestRoutingConfig(
                new Dictionary<string, IReadOnlyCollection<string>>
                {
                    ["screen_opened"] = new[] { "debug" }
                }));

            Assert.That(router.ShouldSendToProvider(new AnalyticsEvent("screen_opened"), new RecordingProvider("debug")), Is.True);
            Assert.That(router.ShouldSendToProvider(new AnalyticsEvent("screen_opened"), new RecordingProvider("firebase")), Is.False);
        }

        [Test]
        public void Mapper_RenamesEventAndParameters()
        {
            var mapper = new DefaultAnalyticsEventMapper(new TestMappingConfig());
            var sourceEvent = new AnalyticsEvent("item_purchased", new Dictionary<string, object>
            {
                ["item_id"] = "pack_1",
                ["debug_only"] = true
            });

            var mappedEvent = mapper.Map(sourceEvent, "firebase");

            Assert.That(mappedEvent.Name, Is.EqualTo("purchase"));
            Assert.That(mappedEvent.Parameters.ContainsKey("item_id"), Is.False);
            Assert.That(mappedEvent.Parameters["item_name"], Is.EqualTo("pack_1"));
            Assert.That(mappedEvent.Parameters.ContainsKey("debug_only"), Is.False);
        }

        [Test]
        public void ConsentStub_AllowsAnalytics()
        {
            var consent = new StubAnalyticsConsentService();

            consent.SetAnalyticsConsent(false);
            consent.SetAttributionConsent(false);
            consent.SetPersonalizedAdsConsent(false);

            Assert.That(consent.CanSendAnalytics, Is.True);
            Assert.That(consent.CanSendAttributionData, Is.True);
            Assert.That(consent.CanSendPersonalizedAdsData, Is.True);
        }
    }

    public sealed class TestRoutingConfig : IAnalyticsRoutingConfig
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyCollection<string>> _rules;

        public TestRoutingConfig(IReadOnlyDictionary<string, IReadOnlyCollection<string>> rules)
        {
            _rules = rules;
        }

        public bool ShouldSendToProvider(string eventName, string providerId)
        {
            return !_rules.TryGetValue(eventName, out var providerIds) || providerIds.Contains(providerId);
        }
    }

    public sealed class TestMappingConfig : IAnalyticsMappingConfig
    {
        public bool TryGetEventMapping(
            string providerId,
            string eventName,
            out string mappedEventName,
            out IReadOnlyDictionary<string, string> parameterMappings)
        {
            if (providerId == "firebase" && eventName == "item_purchased")
            {
                mappedEventName = "purchase";
                parameterMappings = new Dictionary<string, string>
                {
                    ["item_id"] = "item_name",
                    ["debug_only"] = string.Empty
                };
                return true;
            }

            mappedEventName = null;
            parameterMappings = null;
            return false;
        }
    }
}
