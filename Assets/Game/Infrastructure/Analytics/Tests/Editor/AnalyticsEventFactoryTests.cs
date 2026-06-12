using System.Collections.Generic;
using Analytics;
using NUnit.Framework;

namespace AnalyticsTests.Editor
{
    public sealed class AnalyticsEventFactoryTests
    {
        [Test]
        public void CreateEvent_NormalizesNullParameters()
        {
            var factory = new AnalyticsEventFactory();

            var analyticsEvent = factory.CreateEvent("app_started");

            Assert.That(analyticsEvent.Name, Is.EqualTo("app_started"));
            Assert.That(analyticsEvent.Parameters, Is.Not.Null);
            Assert.That(analyticsEvent.Parameters.Count, Is.EqualTo(0));
        }

        [Test]
        public void CreateEvent_CopiesInputParameters()
        {
            var parameters = new Dictionary<string, object>
            {
                ["amount"] = 10
            };
            var factory = new AnalyticsEventFactory();

            var analyticsEvent = factory.CreateEvent("currency_earned", parameters);
            parameters["amount"] = 20;

            Assert.That(analyticsEvent.Parameters["amount"], Is.EqualTo(10));
        }
    }
}
