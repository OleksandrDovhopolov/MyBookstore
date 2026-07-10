using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using Game.Configs.Models;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor.Services
{
    /// <summary>Tests for <see cref="CustomerTrafficConfigValidator"/> hard-override vs request-count warning.</summary>
    public sealed class CustomerTrafficConfigValidatorTests
    {
        private static FakeConfigsService Configs(int requestCount, params DayConfig[] days)
        {
            var configs = new FakeConfigsService();
            var requests = new RequestConfig[requestCount];
            for (var i = 0; i < requestCount; i++) requests[i] = new RequestConfig { Id = $"r{i + 1}" };
            configs.SetAll(requests);
            configs.SetAll(days);
            return configs;
        }

        [Test]
        public void Warns_WhenHardOverrideBelowRequestCount()
        {
            var configs = Configs(3, new DayConfig { Id = "d1", DayIndex = 1, CustomerCount = 2, ApplyModifiers = false });
            var warnings = new CustomerTrafficConfigValidator(configs).Validate();
            Assert.AreEqual(1, warnings.Count);
        }

        [Test]
        public void NoWarn_WhenHardOverrideMeetsRequestCount()
        {
            var configs = Configs(3, new DayConfig { Id = "d1", DayIndex = 1, CustomerCount = 3, ApplyModifiers = false });
            Assert.AreEqual(0, new CustomerTrafficConfigValidator(configs).Validate().Count);
        }

        [Test]
        public void NoWarn_ForModifierDays_EvenBelowRequestCount()
        {
            var configs = Configs(3, new DayConfig { Id = "d1", DayIndex = 1, CustomerCount = 0, ApplyModifiers = true });
            Assert.AreEqual(0, new CustomerTrafficConfigValidator(configs).Validate().Count);
        }
    }
}
