using Book.Sell.Domain;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using Game.Configs.Models;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor.Services
{
    /// <summary>
    /// Tests for <see cref="CustomerTrafficConfigValidator"/>: a day must not ask for more active requests
    /// than it has customers. The requests.json catalog size is deliberately irrelevant — it is a pool.
    /// </summary>
    public sealed class CustomerTrafficConfigValidatorTests
    {
        private static FakeConfigsService Configs(int catalogSize, params DayConfig[] days)
        {
            var configs = new FakeConfigsService();
            var requests = new RequestDefinitionConfig[catalogSize];
            for (var i = 0; i < catalogSize; i++) requests[i] = SalesTestKit.RequestDef($"r{i + 1}");
            configs.SetAll(requests);
            configs.SetAll(days);
            return configs;
        }

        private static SalesTrafficSettings Settings(int defaultCustomers = 5, int defaultRequests = 1)
            => new() { DefaultCustomerCount = defaultCustomers, DefaultActiveRequestCount = defaultRequests };

        [Test]
        public void Warns_WhenDayAsksMoreRequestsThanCustomers()
        {
            var configs = Configs(3, new DayConfig { Id = "d1", DayIndex = 1, CustomerCount = 2, ActiveRequestCount = 3 });
            var warnings = new CustomerTrafficConfigValidator(configs, Settings()).Validate();

            Assert.AreEqual(1, warnings.Count);
            StringAssert.Contains("d1", warnings[0]);
        }

        [Test]
        public void NoWarn_WhenRequestsEqualCustomers()
        {
            var configs = Configs(3, new DayConfig { Id = "d1", DayIndex = 1, CustomerCount = 3, ActiveRequestCount = 3 });
            Assert.AreEqual(0, new CustomerTrafficConfigValidator(configs, Settings()).Validate().Count);
        }

        [Test]
        public void NoWarn_WhenRequestsBelowCustomers()
        {
            var configs = Configs(3, new DayConfig { Id = "d1", DayIndex = 1, CustomerCount = 5, ActiveRequestCount = 1 });
            Assert.AreEqual(0, new CustomerTrafficConfigValidator(configs, Settings()).Validate().Count);
        }

        /// <summary>Regression: a 49-entry catalog used to force a 49-customer day. The pool is not a schedule.</summary>
        [Test]
        public void NoWarn_WhenCatalogIsLargerThanTheDay()
        {
            var configs = Configs(49, new DayConfig { Id = "d1", DayIndex = 1, CustomerCount = 1, ActiveRequestCount = 1 });
            Assert.AreEqual(0, new CustomerTrafficConfigValidator(configs, Settings()).Validate().Count);
        }

        [Test]
        public void UsesDefaults_WhenDayOmitsCounts()
        {
            var configs = Configs(3, new DayConfig { Id = "d1", DayIndex = 1 });
            var warnings = new CustomerTrafficConfigValidator(configs, Settings(defaultCustomers: 1, defaultRequests: 4))
                .Validate();

            Assert.AreEqual(1, warnings.Count);
        }

        [Test]
        public void HardOverrideDay_IsValidatedLikeAnyOther()
        {
            var configs = Configs(3, new DayConfig
            {
                Id = "d1", DayIndex = 1, CustomerCount = 1, ActiveRequestCount = 2, ApplyModifiers = false
            });

            Assert.AreEqual(1, new CustomerTrafficConfigValidator(configs, Settings()).Validate().Count);
        }
    }
}
