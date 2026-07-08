using System;
using System.Collections.Generic;
using System.Threading;
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
    /// <summary>
    /// GAME-6 §Этап 5, B4/B5. Unit-level: the decorator prepends one quest character per scheduled dialogue
    /// (valid ids only, unknown ids warn + skip). Controller-smoke: a scheduled dialogue read from DayConfig
    /// (via DefaultSalesSetupProvider) actually opens through the real controller and plays to the end.
    /// </summary>
    public sealed class QuestSchedulingCustomerSpawnerTests
    {
        private static readonly SalesTuning Tuning = SalesTestKit.FastTuning();

        private static Customer Passive(string id)
            => new(id, new ICustomerStep[] { new ApproachStep(), new PassivePurchaseStep(), new LeaveStep() });

        private static DialogueConfig SingleNodeDialogue(string id) => new()
        {
            Id = id,
            Nodes = new[]
            {
                new DialogueNodeConfig
                {
                    NodeId = "root",
                    Lines = new[] { "line" },
                    Options = new[] { new DialogueOptionConfig { Text = "ok", Next = "end" } }
                }
            }
        };

        // --- decorator unit tests -----------------------------------------------------------

        [Test]
        public void PrependsOneQuestCharacter_PerScheduledDialogue()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { SingleNodeDialogue("dlg") });
            var inner = new StubCustomerSpawner(new List<Customer> { Passive("inner_1"), Passive("inner_2") });
            var spawner = new QuestSchedulingCustomerSpawner(inner, configs);

            var setup = new SalesSessionSetup(1, "loc", Array.Empty<string>(), scheduledDialogueIds: new[] { "dlg" });
            var customers = spawner.BuildCustomers(setup, Tuning, new FakeSalesRandom());

            // The step list is not inspectable (Customer exposes only CurrentStep); the id convention proves
            // the prepend, and the controller-smoke below proves the DialogStep actually runs.
            Assert.AreEqual(3, customers.Count, "inner (2) + 1 quest character.");
            Assert.AreEqual("quest_dlg", customers[0].Id, "Quest character is prepended (arrives first).");
            Assert.AreEqual("inner_1", customers[1].Id);
        }

        [Test]
        public void UnknownDialogueId_WarnsAndSkips()
        {
            var configs = new FakeConfigsService(); // no DialogueConfig registered
            var inner = new StubCustomerSpawner(new List<Customer> { Passive("inner_1") });
            var spawner = new QuestSchedulingCustomerSpawner(inner, configs);

            var setup = new SalesSessionSetup(1, "loc", Array.Empty<string>(), scheduledDialogueIds: new[] { "nope" });

            LogAssert.Expect(LogType.Warning,
                "[Sales.QuestSchedule] scheduled dialogue 'nope' has no DialogueConfig — skipped.");
            var customers = spawner.BuildCustomers(setup, Tuning, new FakeSalesRandom());

            Assert.AreEqual(1, customers.Count, "Unknown id adds no quest character.");
            Assert.AreEqual("inner_1", customers[0].Id);
        }

        [Test]
        public void NoScheduledDialogues_ReturnsInnerUnchanged()
        {
            var configs = new FakeConfigsService();
            var innerList = new List<Customer> { Passive("inner_1") };
            var spawner = new QuestSchedulingCustomerSpawner(new StubCustomerSpawner(innerList), configs);

            var setup = new SalesSessionSetup(1, "loc", Array.Empty<string>()); // no scheduled ids
            var customers = spawner.BuildCustomers(setup, Tuning, new FakeSalesRandom());

            Assert.AreEqual(1, customers.Count);
            Assert.AreEqual("inner_1", customers[0].Id);
        }

        // --- controller-smoke: DayConfig → provider → decorator → real controller ------------

        [Test]
        public void ScheduledDialogueFromDayConfig_OpensThroughController_AndCompletes()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { SalesTestKit.Book("b1") });
            configs.SetAll(new[] { SalesTestKit.Location() });
            configs.SetAll(new[] { SingleNodeDialogue("dlg") });
            configs.SetAll(new[] { new DayConfig { Id = "day_001", DayIndex = 1, ScheduledDialogueIds = new[] { "dlg" } } });

            // Empty inner → the quest character is the only customer, isolating the scheduled path.
            var spawner = new QuestSchedulingCustomerSpawner(
                new StubCustomerSpawner(Array.Empty<Customer>()), configs);

            var controller = new SalesDayController(
                configs,
                new DefaultSalesSetupProvider(configs),  // reads DayConfig.ScheduledDialogueIds via DayConfigLookup
                new RecommendationScoringService(),
                SalesTestKit.LegacyResolver(),
                new FakeSalesRandom(),
                spawner,
                new InteractionLock(),
                Tuning,
                shelfBuilder: new SalesShelfBuilder(configs));

            var dialogueCount = 0;
            string startedId = null;
            controller.DialogueStarted += (_, payload) => { dialogueCount++; startedId = payload.DialogueId; };

            controller.StartDayAsync(1, CancellationToken.None).GetAwaiter().GetResult();
            for (var i = 0; i < 50 && dialogueCount == 0 && controller.Phase == SalesDayPhase.Running; i++)
                controller.Tick(0.1f);

            Assert.AreEqual(1, dialogueCount, "The prepended quest character opened its scheduled dialogue.");
            Assert.AreEqual("dlg", startedId);

            controller.CompleteDialogue();
            for (var i = 0; i < 200 && controller.Phase == SalesDayPhase.Running; i++)
                controller.Tick(0.1f);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, controller.Phase,
                "After CompleteDialogue the quest character finishes and the day becomes concludable.");
        }
    }
}
