using System.Collections.Generic;
using Analytics;
using NUnit.Framework;

namespace AnalyticsTests.Editor
{
    public sealed class DefaultAnalyticsEventValidatorTests
    {
        [Test]
        public void Validate_AcceptsSupportedPrimitiveTypes()
        {
            var validator = CreateValidator();
            var analyticsEvent = new AnalyticsEvent("item_purchased", new Dictionary<string, object>
            {
                ["string_value"] = "item",
                ["int_value"] = 1,
                ["long_value"] = 2L,
                ["float_value"] = 3f,
                ["double_value"] = 4d,
                ["bool_value"] = true
            });

            var isValid = validator.Validate(analyticsEvent, out var error);

            Assert.That(isValid, Is.True, error);
        }

        [TestCase("")]
        [TestCase("BadName")]
        [TestCase("bad-name")]
        public void Validate_RejectsInvalidEventNames(string eventName)
        {
            var validator = CreateValidator();

            var isValid = validator.Validate(new AnalyticsEvent(eventName), out _);

            Assert.That(isValid, Is.False);
        }

        [Test]
        public void Validate_RejectsUnsupportedValueType()
        {
            var validator = CreateValidator();
            var analyticsEvent = new AnalyticsEvent("app_started", new Dictionary<string, object>
            {
                ["bad_value"] = new object()
            });

            var isValid = validator.Validate(analyticsEvent, out _);

            Assert.That(isValid, Is.False);
        }

        [Test]
        public void Validate_RejectsNullValue()
        {
            var validator = CreateValidator();
            var analyticsEvent = new AnalyticsEvent("app_started", new Dictionary<string, object>
            {
                ["bad_value"] = null
            });

            var isValid = validator.Validate(analyticsEvent, out _);

            Assert.That(isValid, Is.False);
        }

        [Test]
        public void Validate_RejectsParameterCountOverLimit()
        {
            var validator = CreateValidator(maxParameterCount: 1);
            var analyticsEvent = new AnalyticsEvent("app_started", new Dictionary<string, object>
            {
                ["first"] = 1,
                ["second"] = 2
            });

            var isValid = validator.Validate(analyticsEvent, out _);

            Assert.That(isValid, Is.False);
        }

        [Test]
        public void Validate_RejectsNamesOverLengthLimit()
        {
            var validator = CreateValidator(maxEventNameLength: 5);

            var isValid = validator.Validate(new AnalyticsEvent("app_started"), out _);

            Assert.That(isValid, Is.False);
        }

        private static DefaultAnalyticsEventValidator CreateValidator(
            int maxEventNameLength = 40,
            int maxParameterCount = 50)
        {
            return new DefaultAnalyticsEventValidator(new TestAnalyticsConfig
            {
                MaxEventNameLengthValue = maxEventNameLength,
                MaxParameterCountValue = maxParameterCount
            });
        }
    }
}
