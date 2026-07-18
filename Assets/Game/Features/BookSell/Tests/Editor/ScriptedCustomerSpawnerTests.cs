using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using Game.Configs.Models;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Book.Sell.Tests.Editor
{
    public sealed class ScriptedCustomerSpawnerTests
    {
        private static readonly SalesTuning Tuning = SalesTestKit.FastTuning();
        private static readonly SalesSessionSetup DayTwoSetup = new(2, "loc", new[] { "book_travel" });

        private static Customer Passive(string id)
            => new(id, new ICustomerStep[] { new ApproachStep(), new PassivePurchaseStep(), new LeaveStep() });

        private static CustomerScriptConfig Script(
            string id,
            int dayIndex = 2,
            string characterId = null,
            ScriptedPassivePurchaseConfig[] attempts = null)
            => new()
            {
                Id = id,
                DayIndex = dayIndex,
                CharacterId = characterId,
                PassiveAttempts = attempts ?? new[]
                {
                    new ScriptedPassivePurchaseConfig { Genre = "Travel", ForceHit = false }
                }
            };

        [Test]
        public void DayScript_ReplacesRegularSlot_AndPreservesTotalCount()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { Script("day2_missed_sale") });
            var inner = new StubCustomerSpawner(new List<Customer>
            {
                Passive("inner_1"),
                Passive("inner_2"),
                Passive("inner_3")
            });

            var spawner = new ScriptedCustomerSpawner(inner, configs);
            var customers = spawner.BuildCustomers(DayTwoSetup, Tuning, new FakeSalesRandom());

            Assert.AreEqual(3, customers.Count, "Scripted customer replaces a regular slot.");
            Assert.AreEqual("script_day2_missed_sale", customers[0].Id);
            Assert.AreEqual("inner_2", customers[1].Id);
            Assert.AreEqual("inner_3", customers[2].Id);
        }

        [Test]
        public void NonMatchingDay_ReturnsInnerUnchanged()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { Script("day2_missed_sale") });
            var inner = new StubCustomerSpawner(new List<Customer> { Passive("inner_1") });

            var spawner = new ScriptedCustomerSpawner(inner, configs);
            var customers = spawner.BuildCustomers(new SalesSessionSetup(3, "loc", Array.Empty<string>()),
                Tuning, new FakeSalesRandom());

            Assert.AreEqual(1, customers.Count);
            Assert.AreEqual("inner_1", customers[0].Id);
        }

        [Test]
        public void ScriptedCustomer_IsFirst_AndCarriesForcedMissPlan()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { Script("day2_missed_sale", characterId: "visitor_day2") });
            var inner = new StubCustomerSpawner(new List<Customer> { Passive("inner_1"), Passive("inner_2") });

            var spawner = new ScriptedCustomerSpawner(inner, configs);
            var customer = spawner.BuildCustomers(DayTwoSetup, Tuning, new FakeSalesRandom())[0];

            Assert.AreEqual("script_day2_missed_sale", customer.Id);
            Assert.AreEqual("visitor_day2", customer.CharacterId);
            Assert.IsTrue(customer.TryConsumeNextScriptedPassiveAttempt(out var attempt));
            Assert.AreEqual("Travel", attempt.Genre);
            Assert.IsFalse(attempt.ForceHit);
            Assert.IsFalse(customer.TryConsumeNextScriptedPassiveAttempt(out _));
        }

        [Test]
        public void EmptyPassiveAttempts_WarnsAndSkips()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { Script("empty", attempts: Array.Empty<ScriptedPassivePurchaseConfig>()) });
            var inner = new StubCustomerSpawner(new List<Customer> { Passive("inner_1") });

            var spawner = new ScriptedCustomerSpawner(inner, configs);

            LogAssert.Expect(LogType.Warning,
                "[Sales.CustomerScript] script 'empty' has no passive attempts; skipped.");
            var customers = spawner.BuildCustomers(DayTwoSetup, Tuning, new FakeSalesRandom());

            Assert.AreEqual(1, customers.Count);
            Assert.AreEqual("inner_1", customers[0].Id);
        }

        [Test]
        public void MoreScriptsThanSlots_WarnsAndSkipsOverflow()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { Script("first"), Script("second") });
            var inner = new StubCustomerSpawner(new List<Customer> { Passive("inner_1") });

            var spawner = new ScriptedCustomerSpawner(inner, configs);

            LogAssert.Expect(LogType.Warning,
                "[Sales.CustomerScript] replacement capacity exhausted for day=2; extra customer scripts skipped.");
            var customers = spawner.BuildCustomers(DayTwoSetup, Tuning, new FakeSalesRandom());

            Assert.AreEqual(1, customers.Count);
            Assert.AreEqual("script_first", customers[0].Id);
        }
    }
}
