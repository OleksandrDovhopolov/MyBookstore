using System.Collections.Generic;
using Book.Sell.Domain;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor.Services
{
    public sealed class CustomerSpawnSchedulerTests
    {
        [Test]
        public void Advance_WaveGate_WaitsForPreviousWaveDoneAndGap()
        {
            var customers = new List<Customer>
            {
                CompletingCustomer("c0"),
                HoldingCustomer("c1"),
                HoldingCustomer("c2"),
                HoldingCustomer("c3")
            };
            var scheduler = new CustomerSpawnScheduler(customers, new[] { 1, 3 }, 0.5f, SalesTestKit.FastTuning());

            scheduler.Advance(0.1f);
            Assert.AreEqual(1, scheduler.SpawnedCount);

            scheduler.Advance(1f);
            Assert.AreEqual(1, scheduler.SpawnedCount, "Next wave must wait until the previous wave is done.");

            Complete(customers[0]);
            scheduler.Advance(0.4f);
            Assert.AreEqual(1, scheduler.SpawnedCount, "Next wave must wait for the configured gap.");

            scheduler.Advance(0.1f);
            Assert.AreEqual(4, scheduler.SpawnedCount);
        }

        [Test]
        public void Advance_PreservesSpawnIntervalInsideWave()
        {
            var customers = new List<Customer>
            {
                HoldingCustomer("c0"),
                HoldingCustomer("c1"),
                HoldingCustomer("c2")
            };
            var tuning = SalesTestKit.FastTuning();
            tuning.SpawnInterval = 0.25f;
            var scheduler = new CustomerSpawnScheduler(customers, new[] { 3 }, 0f, tuning);

            scheduler.Advance(0.1f);
            Assert.AreEqual(1, scheduler.SpawnedCount);

            scheduler.Advance(0.1f);
            Assert.AreEqual(1, scheduler.SpawnedCount);

            scheduler.Advance(0.1f);
            Assert.AreEqual(2, scheduler.SpawnedCount);
        }

        [Test]
        public void Advance_PreservesMaxConcurrentCustomersInsideWave()
        {
            var customers = new List<Customer>
            {
                HoldingCustomer("c0"),
                HoldingCustomer("c1"),
                HoldingCustomer("c2"),
                HoldingCustomer("c3")
            };
            var tuning = SalesTestKit.FastTuning();
            tuning.MaxConcurrentCustomers = 2;
            var scheduler = new CustomerSpawnScheduler(customers, new[] { 4 }, 0f, tuning);

            scheduler.Advance(0.1f);

            Assert.AreEqual(2, scheduler.SpawnedCount);
            Assert.IsFalse(scheduler.NoMoreToSpawn);
        }

        [Test]
        public void Advance_NullWaveSizes_KeepsSingleWaveBehavior()
        {
            var customers = new List<Customer>
            {
                HoldingCustomer("c0"),
                HoldingCustomer("c1"),
                HoldingCustomer("c2")
            };
            var scheduler = new CustomerSpawnScheduler(customers, null, 10f, SalesTestKit.FastTuning());

            scheduler.Advance(0.1f);

            Assert.AreEqual(3, scheduler.SpawnedCount);
            Assert.IsTrue(scheduler.NoMoreToSpawn);
        }

        [Test]
        public void StopSpawning_PreventsNewSpawnsWithoutRollingBackSpawnedCount()
        {
            var customers = new List<Customer>
            {
                HoldingCustomer("c0"),
                HoldingCustomer("c1"),
                HoldingCustomer("c2")
            };
            var tuning = SalesTestKit.FastTuning();
            tuning.SpawnInterval = 0.25f;
            var scheduler = new CustomerSpawnScheduler(customers, new[] { 3 }, 0f, tuning);

            scheduler.Advance(0.1f);
            Assert.AreEqual(1, scheduler.SpawnedCount);

            scheduler.StopSpawning();
            scheduler.Advance(5f);

            Assert.AreEqual(1, scheduler.SpawnedCount);
            Assert.IsTrue(scheduler.NoMoreToSpawn);
        }

        private static Customer HoldingCustomer(string id)
            => new(id, new ICustomerStep[] { new HoldStep() });

        private static Customer CompletingCustomer(string id)
            => new(id, new ICustomerStep[] { new CompletingStep() });

        private static void Complete(Customer customer)
            => customer.Tick(
                SalesTestKit.Context(SalesTestKit.Shelf(), SalesTestKit.Location(), new RecordingSink()),
                0f);

        private sealed class HoldStep : ICustomerStep
        {
            public void Enter(Customer self, CustomerContext ctx)
            {
                self.SetPhase(CustomerPhase.Approaching, ctx);
            }

            public StepStatus Tick(Customer self, CustomerContext ctx, float dt) => StepStatus.Running;
            public void Exit(Customer self, CustomerContext ctx) { }
        }

        private sealed class CompletingStep : ICustomerStep
        {
            public void Enter(Customer self, CustomerContext ctx)
            {
                self.SetPhase(CustomerPhase.Approaching, ctx);
            }

            public StepStatus Tick(Customer self, CustomerContext ctx, float dt) => StepStatus.Completed;
            public void Exit(Customer self, CustomerContext ctx) { }
        }
    }
}
