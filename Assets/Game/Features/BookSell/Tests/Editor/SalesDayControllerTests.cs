using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using Cysharp.Threading.Tasks;
using Dialogue;
using Game.Configs.Models;
using Game.DayCycle.Day;
using Game.Inventory.API;
using Game.Preparation.Services;
using Game.Resources.API;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Book.Sell.Tests.Editor
{
    public sealed class SalesDayControllerTests
    {
        // ----- builders -----

        private static Customer Passive(string id)
            => new(id, new ICustomerStep[] { new ApproachStep(), new PassivePurchaseStep(), new LeaveStep() });

        // Approach + Leave only: no purchase step, so the shelf never depletes and spawning is gated
        // purely by the concurrency cap (used by the MaxConcurrentCustomers test).
        private static Customer ApproachLeave(string id)
            => new(id, new ICustomerStep[] { new ApproachStep(), new LeaveStep() });

        private static Customer Active(string id, ActiveRequestRuntime req)
            => new(id, new ICustomerStep[] { new ApproachStep(), new ActiveRequestStep(req), new LeaveStep() });

        // Active request with the full closing tail, so CompletePurchaseStep actually runs after the
        // recommendation resolves (needed to assert the visit-completion event).
        private static Customer ActiveWithCompletion(string id, ActiveRequestRuntime req)
            => new(id, new ICustomerStep[]
            {
                new ApproachStep(), new ActiveRequestStep(req), new CompletePurchaseStep(), new LeaveStep()
            });

        // Approach + scripted dialogue + Leave. The dialogue holds the interaction lock until the
        // controller resolves it via CompleteDialogue(); no purchase steps.
        private static Customer Dialog(string id, string dialogueId = "dlg")
            => new(id, new ICustomerStep[] { new ApproachStep(), new DialogStep(new DialoguePayload(dialogueId)), new LeaveStep() });

        // Ticks until a dialogue opens (DialogueStarted fired) or we give up. No CurrentDialogue getter
        // exists, so detection is via the event — same shape as DriveUntilActive/CurrentRequest.
        private static void DriveUntilDialogue(SalesDayController c, System.Func<int> firedCount, int maxTicks = 50)
        {
            for (var i = 0; i < maxTicks && firedCount() == 0 && c.Phase == SalesDayPhase.Running; i++) c.Tick(0.1f);
        }

        private static SalesDayController Build(
            BookConfig[] books, RequestDefinitionConfig[] requests, LocationConfig location, IReadOnlyList<Customer> customers,
            SalesTuning tuning = null,
            ISalesDayCommitService commitService = null,
            IPassivePurchaseResolver passiveResolver = null,
            DayConfig[] dayConfigs = null,
            IDeliveredDialoguesService delivered = null)
        {
            var configs = new FakeConfigsService();
            configs.SetAll(books);
            configs.SetAll(requests);
            configs.SetAll(new[] { location });
            configs.SetAll(dayConfigs ?? Array.Empty<DayConfig>());

            var shelfBuilder = new SalesShelfBuilder(configs);

            return new SalesDayController(
                configs,
                new DefaultSalesSetupProvider(configs),
                new ActiveRequestScoringService(new BookConditionRequestEvaluator()),
                passiveResolver ?? SalesTestKit.LegacyResolver(),
                new FakeSalesRandom(),
                new StubCustomerSpawner(customers),
                new InteractionLock(),
                tuning ?? SalesTestKit.FastTuning(),
                shelfBuilder: shelfBuilder,
                commitService: commitService,
                delivered: delivered);
        }

        private static ActiveRequestRuntime ConditionRequest(string id, string quality)
        {
            var request = new RequestDefinitionConfig
            {
                Id = id,
                Enabled = true,
                Conditions = new RequestConditionGroup
                {
                    All = new[]
                    {
                        new RequestCondition
                        {
                            Type = "qualities",
                            Operator = "contains",
                            Value = JToken.FromObject(quality)
                        }
                    }
                }
            };

            return ActiveRequestRuntime.FromCondition(request, $"ALL: qualities contains {quality}");
        }

        private static ActiveRequestRuntime GenreRequest(string id, params string[] genres)
        {
            var request = new RequestDefinitionConfig
            {
                Id = id,
                Enabled = true,
                Conditions = new RequestConditionGroup
                {
                    All = new[]
                    {
                        new RequestCondition
                        {
                            Type = "genres",
                            Operator = "containsAny",
                            Value = JToken.FromObject(genres)
                        }
                    }
                }
            };

            return ActiveRequestRuntime.FromCondition(
                request,
                $"ALL: genres containsAny [{string.Join(", ", genres)}]",
                genres);
        }

        // Records the day result handed to the transactional commit at day completion.
        private sealed class RecordingSalesDayCommitService : ISalesDayCommitService
        {
            public int CommitCalls { get; private set; }
            public SalesDayResult LastResult { get; private set; }

            public UniTask CommitAsync(SalesDayResult result, CancellationToken ct)
            {
                CommitCalls++;
                LastResult = result;
                return UniTask.CompletedTask;
            }
        }

        private sealed class RecordingDeliveredDialogues : IDeliveredDialoguesService
        {
            public event Action Changed;
            public int DiscardCalls { get; private set; }
            public bool IsDelivered(string dialogueId) => false;
            public UniTask MarkDeliveredAsync(string dialogueId, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask MarkDeliveredDeferredAsync(string dialogueId, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask CommitAsync(CancellationToken ct) => UniTask.CompletedTask;
            public void DiscardDeferred() => DiscardCalls++;
        }

        private static void StartDay(SalesDayController c)
            => c.StartDayAsync(1, CancellationToken.None).GetAwaiter().GetResult();

        private static int SpawnedCount(IReadOnlyList<Customer> customers)
            => customers.Count(x => x.Phase != CustomerPhase.Spawned);

        // Drives the day until it stops being Running (i.e. reaches ReadyToClose). The day no longer
        // auto-completes; tests assert at ReadyToClose, then call ConcludeDay() when they need the
        // published result.
        private static void Run(SalesDayController c, int maxTicks = 200)
        {
            for (var i = 0; i < maxTicks && c.Phase == SalesDayPhase.Running; i++) c.Tick(0.1f);
        }

        private static void DriveUntilActive(SalesDayController c, int maxTicks = 50)
        {
            for (var i = 0; i < maxTicks && c.CurrentRequest == null && c.Phase == SalesDayPhase.Running; i++) c.Tick(0.1f);
        }

        private sealed class RecordingInventoryService : IInventoryService
        {
            private readonly List<InventoryItem> _items = new();

            public List<(string ItemId, int Amount)> RemoveCalls { get; } = new();
            public List<string> OperationLog { get; }

            public event System.Action<InventoryChangeEvent> Changed;

            public RecordingInventoryService(List<string> operationLog = null)
            {
                OperationLog = operationLog ?? new List<string>();
            }

            public static RecordingInventoryService WithBooks(IEnumerable<BookConfig> books)
            {
                var inventory = new RecordingInventoryService();
                if (books == null) return inventory;

                foreach (var book in books)
                {
                    if (book == null || string.IsNullOrEmpty(book.Id)) continue;
                    inventory.Seed(book.Id, InventoryCategories.Book);
                }
                return inventory;
            }

            public RecordingInventoryService Seed(string itemId, string categoryId, int count = 1)
            {
                if (!_items.Any(i => i.ItemId == itemId))
                    _items.Add(new InventoryItem(itemId, categoryId, count));
                return this;
            }

            public IReadOnlyList<InventoryItem> GetAll() => _items.ToList();

            public IReadOnlyList<InventoryItem> GetByCategory(string categoryId)
                => _items.Where(i => i.CategoryId == categoryId).ToList();

            public bool Has(string itemId) => GetCount(itemId) > 0;

            public int GetCount(string itemId)
                => _items.FirstOrDefault(i => i.ItemId == itemId)?.Count ?? 0;

            public UniTask AddAsync(string itemId, string categoryId, int amount, CancellationToken ct)
            {
                Seed(itemId, categoryId, amount);
                Changed?.Invoke(new InventoryChangeEvent(categoryId, itemId, InventoryChangeKind.Added, amount));
                return UniTask.CompletedTask;
            }

            public UniTask AddBatchAsync(IEnumerable<InventoryItem> items, CancellationToken ct)
            {
                foreach (var item in items)
                    Seed(item.ItemId, item.CategoryId, item.Count);
                return UniTask.CompletedTask;
            }

            public UniTask<bool> RemoveAsync(string itemId, int amount, CancellationToken ct)
            {
                RemoveCalls.Add((itemId, amount));
                OperationLog.Add($"inventory:{itemId}");

                var existing = _items.FirstOrDefault(i => i.ItemId == itemId);
                if (existing == null || existing.Count < amount)
                    return UniTask.FromResult(false);

                _items.Remove(existing);
                var newCount = existing.Count - amount;
                if (newCount > 0)
                    _items.Add(new InventoryItem(itemId, existing.CategoryId, newCount));

                Changed?.Invoke(new InventoryChangeEvent(existing.CategoryId, itemId, InventoryChangeKind.Removed, newCount));
                return UniTask.FromResult(true);
            }
        }

        private sealed class RecordingSalesGoldCollector : ISalesGoldCollector
        {
            public bool ResetCalled { get; private set; }
            public bool FlushCalled { get; private set; }
            public List<(int Day, string BookId, int Amount, string Source)> CollectCalls { get; } = new();

            public void Reset()
            {
                ResetCalled = true;
            }

            public void CollectSaleGold(int day, string bookId, int amount, string source)
            {
                CollectCalls.Add((day, bookId, amount, source));
            }

            public UniTask FlushAsync(CancellationToken ct)
            {
                FlushCalled = true;
                return UniTask.CompletedTask;
            }
        }

        private sealed class RecordingResourcesService : IResourcesService
        {
            private readonly Dictionary<string, int> _amounts = new(StringComparer.Ordinal);

            public List<(string ResourceId, int Amount, string Reason)> AddCalls { get; } = new();

            public event Action<ResourceChangeEvent> Changed;

            public IReadOnlyDictionary<string, int> GetAll() => _amounts;

            public int GetAmount(string resourceId)
                => !string.IsNullOrEmpty(resourceId) && _amounts.TryGetValue(resourceId, out var amount) ? amount : 0;

            public bool Has(string resourceId, int amount)
                => amount <= 0 || GetAmount(resourceId) >= amount;

            public UniTask AddAsync(string resourceId, int amount, string reason, CancellationToken ct)
            {
                AddCalls.Add((resourceId, amount, reason));
                if (string.IsNullOrEmpty(resourceId) || amount <= 0) return UniTask.CompletedTask;

                var old = GetAmount(resourceId);
                var next = old + amount;
                _amounts[resourceId] = next;
                Changed?.Invoke(new ResourceChangeEvent(resourceId, old, next, amount, reason));
                return UniTask.CompletedTask;
            }

            public UniTask<bool> RemoveAsync(string resourceId, int amount, string reason, CancellationToken ct)
                => UniTask.FromResult(false);
        }

        private sealed class StaticPreparationInventoryProvider : IPreparationInventoryProvider
        {
            private readonly IReadOnlyList<BookConfig> _ownedBooks;

            public StaticPreparationInventoryProvider(IReadOnlyList<BookConfig> ownedBooks)
                => _ownedBooks = ownedBooks;

            public IReadOnlyList<BookConfig> GetOwnedBooks() => _ownedBooks;
        }

        private sealed class FakeDayProgressService : IDayProgressService
        {
            public event System.Action<DayProgressState> PhaseChanged;

            public DayProgressState Current { get; } = new();

            public UniTask<DayProgressState> LoadAsync(CancellationToken ct) => UniTask.FromResult(Current);

            public UniTask SetPhaseAsync(DayPhase phase, CancellationToken ct)
            {
                Current.CurrentPhase = phase;
                PhaseChanged?.Invoke(Current);
                return UniTask.CompletedTask;
            }

            public UniTask MarkCurrentDayCompletedAsync(CancellationToken ct)
            {
                Current.CompletedDays.Add(Current.CurrentDay);
                Current.CurrentPhase = DayPhase.Results;
                PhaseChanged?.Invoke(Current);
                return UniTask.CompletedTask;
            }

            public UniTask AdvanceToNextDayAsync(CancellationToken ct)
            {
                Current.CurrentDay++;
                Current.CurrentPhase = DayPhase.Morning;
                PhaseChanged?.Invoke(Current);
                return UniTask.CompletedTask;
            }

            public UniTask SaveAsync(CancellationToken ct) => UniTask.CompletedTask;
        }

        // ----- tests -----

        [Test]
        public void StartDay_DiscardsDeferredDeliveredBeforeSpawning()
        {
            var delivered = new RecordingDeliveredDialogues();
            var c = Build(
                new[] { SalesTestKit.Book("b1") },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(),
                new List<Customer>(),
                delivered: delivered);

            StartDay(c);

            Assert.AreEqual(1, delivered.DiscardCalls);
        }

        [Test]
        public void Dialog_AcquiresLock_FiresDialogueStarted_PausesDay()
        {
            var expected = Dialog("c1");
            var c = Build(
                new[] { SalesTestKit.Book("b1") },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(),
                new List<Customer> { expected });

            Customer startedCustomer = null;
            DialoguePayload startedPayload = null;
            var count = 0;
            c.DialogueStarted += (cust, payload) => { startedCustomer = cust; startedPayload = payload; count++; };

            StartDay(c);
            DriveUntilDialogue(c, () => count);

            Assert.AreEqual(1, count, "Dialogue opened exactly once.");
            Assert.AreSame(expected, startedCustomer, "Event carries the dialogue's customer.");
            Assert.AreEqual("dlg", startedPayload.DialogueId);
            Assert.AreEqual(SalesDayPhase.Running, c.Phase);
            Assert.AreEqual(CustomerPhase.InDialogue, expected.Phase);

            // Lock is held → the day is paused: extra ticks neither re-fire nor advance the day.
            for (var i = 0; i < 10; i++) c.Tick(0.1f);
            Assert.AreEqual(1, count, "No duplicate DialogueStarted while paused.");
            Assert.AreEqual(SalesDayPhase.Running, c.Phase);
        }

        [Test]
        public void CompleteDialogue_ResumesDay_CustomerFinishes()
        {
            var c = Build(
                new[] { SalesTestKit.Book("b1") },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(),
                new List<Customer> { Dialog("c1") });

            var count = 0;
            c.DialogueStarted += (_, _) => count++;

            StartDay(c);
            DriveUntilDialogue(c, () => count);
            Assert.AreEqual(1, count);

            c.CompleteDialogue();
            Run(c);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase, "Lock released → customer finishes and the day is closable.");
        }

        [Test]
        public void CompleteDialogue_NoOpenDialogue_IsIgnored()
        {
            var c = Build(
                new[] { SalesTestKit.Book("b1") },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(),
                new List<Customer> { Passive("c1") });

            StartDay(c);

            LogAssert.Expect(LogType.Warning, "[Sales.Day] CompleteDialogue with no open dialogue — ignored.");
            Assert.DoesNotThrow(() => c.CompleteDialogue());
            Assert.AreEqual(SalesDayPhase.Running, c.Phase);
        }

        [Test]
        public void ForceCompleteDay_DuringDialogue_DropsState()
        {
            var c = Build(
                new[] { SalesTestKit.Book("b1") },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(),
                new List<Customer> { Dialog("c1") });

            var count = 0;
            c.DialogueStarted += (_, _) => count++;

            StartDay(c);
            DriveUntilDialogue(c, () => count);
            Assert.AreEqual(1, count);

            c.ForceCompleteDay(zeroOut: false);

            Assert.AreEqual(SalesDayPhase.Completed, c.Phase);
            Assert.DoesNotThrow(() => c.Tick(0.1f), "Tick short-circuits on the completed phase; no hang.");
        }

        [Test]
        public void QuestCharacterArchetype_PlanRunsEndToEnd_DialogThenPassiveThenLeave()
        {
            var tuning = SalesTestKit.FastTuning();
            var random = new FakeSalesRandom();
            var payload = new DialoguePayload("dlg");
            var arch = new QuestCharacterArchetype(payload, passiveCount: 1);

            // Build the plan THROUGH the archetype + CustomerPlanBuilder → production shape
            // Approach → DialogStep → PassivePurchase → CompletePurchase → Leave.
            var customer = CustomerPlanBuilder.Build(
                "c1", tuning, random,
                () => arch.BuildMiddle(new SalesSessionSetup(1, "loc", new[] { "b1" }), tuning, random));

            var c = Build(
                new[] { SalesTestKit.Book("b1") },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(),
                new List<Customer> { customer },
                tuning);

            DialoguePayload started = null;
            var dialogueCount = 0;
            var passiveSales = 0;
            var purchaseCompletedCount = -1;
            c.DialogueStarted += (_, p) => { started = p; dialogueCount++; };
            c.PassiveSaleHappened += _ => passiveSales++;
            c.CustomerPurchaseCompleted += (_, count) => purchaseCompletedCount = count;

            StartDay(c);
            DriveUntilDialogue(c, () => dialogueCount);

            Assert.AreEqual(1, dialogueCount, "The archetype's DialogStep opened the dialogue.");
            Assert.AreEqual("dlg", started.DialogueId);

            c.CompleteDialogue();
            Run(c);

            // Proves Dialog → Passive → CompletePurchase → Leave, not just that the day closed.
            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            Assert.AreEqual(1, c.AccumulatedResult.SalesCount, "Passive sale ran after the dialogue.");
            Assert.AreEqual(1, passiveSales);
            Assert.AreEqual(1, purchaseCompletedCount, "Visit completed with 1 purchased book.");
        }

        [Test]
        public void CompleteDialogue_AfterForceCompleteDay_IsNoOp()
        {
            var c = Build(
                new[] { SalesTestKit.Book("b1") },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(),
                new List<Customer> { Dialog("c1") });

            var count = 0;
            c.DialogueStarted += (_, _) => count++;

            StartDay(c);
            DriveUntilDialogue(c, () => count);
            c.ForceCompleteDay(zeroOut: false);

            // Async UI closes late, after the day was force-completed: dialogue state is already dropped.
            LogAssert.Expect(LogType.Warning, "[Sales.Day] CompleteDialogue with no open dialogue — ignored.");
            Assert.DoesNotThrow(() => c.CompleteDialogue());
            Assert.AreEqual(SalesDayPhase.Completed, c.Phase);
        }

        [Test]
        public void SalesShelfBuilder_BuildsShelfFromBookIds()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[]
            {
                SalesTestKit.Book("b1"),
                SalesTestKit.Book("b2")
            });
            var builder = new SalesShelfBuilder(configs);

            var shelf = builder.Build(new[] { "b1", "b2" });

            Assert.AreEqual(2, shelf.Books.Count);
            Assert.AreEqual("b1", shelf.Books[0].Config.Id);
            Assert.AreEqual("b2", shelf.Books[1].Config.Id);
        }

        [Test]
        public void SalesShelfBuilder_MissingBookConfig_SkipsAndLogsWarning()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { SalesTestKit.Book("b1") });
            var builder = new SalesShelfBuilder(configs);

            LogAssert.Expect(LogType.Warning, "[Sales.Day] BookConfig 'missing' not found - skipping.");

            var shelf = builder.Build(new[] { "b1", "missing" });

            Assert.AreEqual(1, shelf.Books.Count);
            Assert.AreEqual("b1", shelf.Books[0].Config.Id);
        }

        [Test]
        public void SalesShelfBuilder_EmptyOrNullIds_ReturnsEmptyShelf()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { SalesTestKit.Book("b1") });
            var builder = new SalesShelfBuilder(configs);

            Assert.AreEqual(0, builder.Build(Array.Empty<string>()).Books.Count);
            Assert.AreEqual(0, builder.Build(null).Books.Count);
        }

        [Test]
        public void SalesGoldCollector_ZeroOrNegativeAmount_DoesNothing()
        {
            var resources = new RecordingResourcesService();
            var collector = new SalesGoldCollector(resources);

            collector.CollectSaleGold(1, "b1", 0, "test");
            collector.CollectSaleGold(1, "b1", -5, "test");
            collector.FlushAsync(CancellationToken.None).GetAwaiter().GetResult();

            CollectionAssert.IsEmpty(resources.AddCalls);
            Assert.AreEqual(0, resources.GetAmount(ResourceIds.Gold));
        }

        [Test]
        public void SalesGoldCollector_AddsGoldWithSaleReason()
        {
            var resources = new RecordingResourcesService();
            var collector = new SalesGoldCollector(resources);

            collector.CollectSaleGold(2, "b1", 80, "active");
            collector.FlushAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(80, resources.GetAmount(ResourceIds.Gold));
            Assert.AreEqual(1, resources.AddCalls.Count);
            Assert.AreEqual(ResourceIds.Gold, resources.AddCalls[0].ResourceId);
            Assert.AreEqual(80, resources.AddCalls[0].Amount);
            Assert.AreEqual("sales_day_2_active_b1", resources.AddCalls[0].Reason);
        }

        [Test]
        public void SalesGoldCollector_MissingResourcesService_LogsError()
        {
            var collector = new SalesGoldCollector(null);

            LogAssert.Expect(LogType.Error,
                "[Sales.Day] cannot collect 80 gold for sold book 'b1' (test): IResourcesService is not available.");

            collector.CollectSaleGold(1, "b1", 80, "test");
            collector.FlushAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        [Test]
        public void SalesGoldCollector_Flush_CanBeCalledRepeatedly()
        {
            var resources = new RecordingResourcesService();
            var collector = new SalesGoldCollector(resources);

            collector.CollectSaleGold(1, "b1", 80, "passive");
            collector.FlushAsync(CancellationToken.None).GetAwaiter().GetResult();
            collector.FlushAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(1, resources.AddCalls.Count);
            Assert.AreEqual(80, resources.GetAmount(ResourceIds.Gold));
        }

        [Test]
        public void DayCompletion_CommitsResultOnce_BeforePublishing()
        {
            var commit = new RecordingSalesDayCommitService();
            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi", price: 80) },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                new List<Customer> { Passive("c1") },
                commitService: commit);

            var committedBeforePublish = false;
            c.DayCompleted += _ => committedBeforePublish = commit.CommitCalls == 1;

            StartDay(c);
            Run(c);
            c.ConcludeDay();

            Assert.AreEqual(1, commit.CommitCalls, "The day result is committed exactly once at completion.");
            Assert.AreEqual(1, commit.LastResult.SalesCount);
            Assert.AreEqual(BookConfig.FixedPriceGold, commit.LastResult.GoldEarned);
            Assert.IsTrue(committedBeforePublish, "Commit runs before DayCompleted is emitted.");
        }

        [Test]
        public void Spawning_RespectsMaxConcurrentCustomers_AndRefillsWhenSlotFrees()
        {
            // 10 customers, cap 3. Approach+Leave only (no purchase) so the shelf never depletes and
            // every customer eventually reaches Done — letting the spawner refill freed slots.
            var customers = new List<Customer>();
            for (var i = 0; i < 10; i++) customers.Add(ApproachLeave($"c{i}"));

            var tuning = SalesTestKit.FastTuning();   // zero durations, SpawnInterval 0
            tuning.MaxConcurrentCustomers = 3;

            var c = Build(
                new[] { SalesTestKit.Book("b1") }, Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(), customers, tuning: tuning);

            StartDay(c);

            var maxPresent = 0;
            for (var i = 0; i < 200 && c.Phase == SalesDayPhase.Running; i++)
            {
                c.Tick(0.1f);
                // Present = spawned and not yet Done. The cap must never be exceeded.
                var present = customers.Count(x => !x.IsDone && x.Phase != CustomerPhase.Spawned);
                Assert.LessOrEqual(present, 3, "More than 3 customers were present at once.");
                if (present > maxPresent) maxPresent = present;
            }

            Assert.AreEqual(3, maxPresent, "Cap of 3 was never reached — the gate is not being exercised.");
            Assert.IsTrue(customers.All(x => x.IsDone), "Not all customers were eventually served.");
            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
        }

        [Test]
        public void Spawning_WaveGate_WaitsForPreviousWaveDoneAndGap()
        {
            var customers = new List<Customer>
            {
                ApproachLeave("c0"),
                ApproachLeave("c1"),
                ApproachLeave("c2"),
                ApproachLeave("c3")
            };
            var tuning = SalesTestKit.FastTuning();
            var day = new DayConfig { Id = "d1", DayIndex = 1, WaveSizes = new[] { 1, 3 }, WaveGapSeconds = 0.5f };

            var c = Build(
                new[] { SalesTestKit.Book("b1") }, Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(), customers, tuning: tuning, dayConfigs: new[] { day });

            StartDay(c);

            c.Tick(0.1f);
            Assert.AreEqual(1, SpawnedCount(customers), "First wave should contain only Eddi's slot.");

            for (var i = 0; i < 5; i++)
                c.Tick(0.1f);

            Assert.AreEqual(1, SpawnedCount(customers), "Second wave should wait for the configured gap.");

            c.Tick(0.1f);
            Assert.AreEqual(4, SpawnedCount(customers), "Second wave should open after the gap passes.");
        }

        [Test]
        public void StartDay_NoCustomers_BecomesReadyToClose_ThenConcludes()
        {
            var c = Build(
                new[] { SalesTestKit.Book("b1") }, Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(), new List<Customer>());

            var readyToClose = false;
            var completed = false;
            c.DayReadyToClose += () => readyToClose = true;
            c.DayCompleted += _ => completed = true;

            StartDay(c);
            Run(c);

            // No customers → day is immediately closable, but does NOT auto-complete.
            Assert.IsTrue(readyToClose);
            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            Assert.IsFalse(completed, "Day waits for the player to close the shop.");

            c.ConcludeDay();

            Assert.IsTrue(completed);
            Assert.IsTrue(c.IsDayCompleted);
        }

        [Test]
        public void SinglePassiveCustomer_BuysOneBook_ThenLeaves()
        {
            // Two books so the day ends via "all customers done" (one book remains),
            // not via "all sold out" — that lets the customer actually reach Done (CustomersServed).
            var c = Build(
                new[]
                {
                    SalesTestKit.Book("b1", genre: "sci-fi", price: 80),
                    SalesTestKit.Book("b2", genre: "sci-fi", price: 80)
                },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                new List<Customer> { Passive("c1") });

            var passive = 0;
            c.PassiveSaleHappened += _ => passive++;

            StartDay(c);
            Run(c);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            Assert.AreEqual(1, passive, "One PassivePurchaseStep → one book bought.");
            Assert.AreEqual(1, c.AccumulatedResult.SalesCount);
            Assert.AreEqual(BookConfig.FixedPriceGold, c.AccumulatedResult.GoldEarned);
            Assert.AreEqual(1, c.AccumulatedResult.CustomersServed);
        }

        [Test]
        public void StartDay_RaisesShelfChanged_AfterShelfIsBuilt()
        {
            var c = Build(
                new[] { SalesTestKit.Book("b1"), SalesTestKit.Book("b2") },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(),
                new List<Customer>());

            var changes = 0;
            c.ShelfChanged += () => changes++;

            StartDay(c);

            Assert.AreEqual(1, changes);
            Assert.AreEqual(2, c.Shelf.Books.Count);
        }

        [Test]
        public void RecommendBook_SuccessfulSale_RaisesShelfChanged()
        {
            var reqA = SalesTestKit.ActiveRequest("reqA");
            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi", price: 80) },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(),
                new List<Customer> { Active("c1", reqA) });

            StartDay(c);
            DriveUntilActive(c);

            var changes = 0;
            c.ShelfChanged += () => changes++;

            c.RecommendBook("b1");

            Assert.AreEqual(1, changes);
            Assert.AreEqual(ShelfBookState.SoldOut, c.Shelf.Find("b1").State);
        }

        [Test]
        public void RecommendBook_SuccessfulSale_AccumulatesGoldInResult()
        {
            var reqA = SalesTestKit.ActiveRequest("reqA");
            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi", price: 80) },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(),
                new List<Customer> { Active("c1", reqA) });

            StartDay(c);
            DriveUntilActive(c);

            c.RecommendBook("b1");

            // Provisional only: gold lands in the day result, not the wallet, until the day commits.
            Assert.AreEqual(BookConfig.FixedPriceGold, c.AccumulatedResult.GoldEarned);
            CollectionAssert.Contains(c.AccumulatedResult.SoldBookIds, "b1");
        }

        [Test]
        public void ActiveSale_CountsAsPurchasedBook_CompletionFiresWithCountOne()
        {
            // Two books so the day ends via "all customers done" (b2 remains), letting the customer
            // reach CompletePurchase/Done after the active sale of b1.
            var reqA = SalesTestKit.ActiveRequest("reqA");
            var c = Build(
                new[]
                {
                    SalesTestKit.Book("b1", genre: "sci-fi", price: 80),
                    SalesTestKit.Book("b2", genre: "sci-fi", price: 80)
                },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(),
                new List<Customer> { ActiveWithCompletion("c1", reqA) });

            (Customer customer, int count)? completion = null;
            c.CustomerPurchaseCompleted += (cust, n) => completion = (cust, n);

            StartDay(c);
            DriveUntilActive(c);
            c.RecommendBook("b1");
            Run(c);

            Assert.IsTrue(completion.HasValue, "An active sale must trigger the visit-completion bubble.");
            Assert.AreEqual(1, completion.Value.count, "The active sale counts as one purchased book.");
        }

        [Test]
        public void SkippedActiveRequest_BuysNothing_NoCompletion()
        {
            var reqA = SalesTestKit.ActiveRequest("reqA");
            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi", price: 80) },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(),
                new List<Customer> { ActiveWithCompletion("c1", reqA) });

            var completionFired = false;
            c.CustomerPurchaseCompleted += (_, _) => completionFired = true;

            StartDay(c);
            DriveUntilActive(c);
            c.SkipCurrentRequest();
            Run(c);

            Assert.IsFalse(completionFired, "0 books bought → CompletePurchase is skipped, no completion bubble.");
            Assert.AreEqual(0, c.AccumulatedResult.GoldEarned);
            CollectionAssert.IsEmpty(c.AccumulatedResult.SoldBookIds);
        }

        // NOTE: per-sale persistence (inventory removal + persistent shelf state) moved to the
        // transactional commit at day completion; it is covered by SalesDayCommitServiceTests, not here.

        [Test]
        public void SkipCurrentRequest_DoesNotRaiseShelfChanged()
        {
            var reqA = SalesTestKit.ActiveRequest("reqA");
            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi", price: 80) },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(),
                new List<Customer> { Active("c1", reqA) });

            StartDay(c);
            DriveUntilActive(c);

            var changes = 0;
            c.ShelfChanged += () => changes++;

            c.SkipCurrentRequest();

            Assert.AreEqual(0, changes);
            Assert.AreEqual(ShelfBookState.Available, c.Shelf.Find("b1").State);
        }

        [Test]
        public void PassiveSale_RaisesShelfChanged()
        {
            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi", price: 80) },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                new List<Customer> { Passive("c1") });

            StartDay(c);

            var changes = 0;
            c.ShelfChanged += () => changes++;

            Run(c);

            Assert.AreEqual(1, changes);
            Assert.AreEqual(ShelfBookState.SoldOut, c.Shelf.Find("b1").State);
        }

        [Test]
        public void PassiveSale_AccumulatesGoldInResult()
        {
            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi", price: 80) },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                new List<Customer> { Passive("c1") });

            StartDay(c);
            Run(c);

            Assert.AreEqual(BookConfig.FixedPriceGold, c.AccumulatedResult.GoldEarned);
            CollectionAssert.Contains(c.AccumulatedResult.SoldBookIds, "b1");
        }

        [Test]
        public void PreparationInventoryProvider_EmptyInventory_ReturnsEmptyList()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { SalesTestKit.Book("b1"), SalesTestKit.Book("b2") });
            var provider = new DayProgressInventoryProvider(new RecordingInventoryService(), configs);

            LogAssert.Expect(LogType.Warning, "[Preparation.Inventory] inventory book category is empty - no owned books available.");

            var owned = provider.GetOwnedBooks();

            CollectionAssert.IsEmpty(owned);
        }

        [Test]
        public void PreparationInventoryProvider_ReturnsOnlyBooksOwnedInInventory()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { SalesTestKit.Book("b1"), SalesTestKit.Book("b2") });
            var inventory = new RecordingInventoryService().Seed("b2", InventoryCategories.Book);
            var provider = new DayProgressInventoryProvider(inventory, configs);

            var owned = provider.GetOwnedBooks();

            CollectionAssert.AreEqual(new[] { "b2" }, owned.Select(b => b.Id).ToArray());
        }

        [Test]
        public void PreparationSession_IncludesCatalogGenresWithZeroOwnedBooks()
        {
            var sciFi = SalesTestKit.Book("b1", genre: "sci-fi");
            var romance = SalesTestKit.Book("b2", genre: "romance");
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { sciFi, romance });

            var service = new PreparationSessionService(
                new FakeSaveService(),
                new FakeDayProgressService(),
                new StaticPreparationInventoryProvider(new[] { sciFi }),
                new RecordingShelfStateService(),
                configs);

            var items = service.StartOrResumeAsync(CancellationToken.None).GetAwaiter().GetResult();

            var byGenre = items.ToDictionary(item => item.Genre, StringComparer.OrdinalIgnoreCase);
            Assert.AreEqual(1, byGenre["sci-fi"].Available);
            Assert.AreEqual(0, byGenre["romance"].Available);
            Assert.AreEqual(0, byGenre["romance"].Quantity);
        }

        [Test]
        public void PreparationSession_SetSelectedBookIds_PreservesExactIdsThroughConfirm()
        {
            var books = new[]
            {
                SalesTestKit.Book("b1", genre: "Fact"),
                SalesTestKit.Book("b2", genre: "Travel"),
                SalesTestKit.Book("b3", genre: "Fantasy")
            };
            var configs = new FakeConfigsService();
            configs.SetAll(books);
            var shelfState = new RecordingShelfStateService();

            var service = new PreparationSessionService(
                new FakeSaveService(),
                new FakeDayProgressService(),
                new StaticPreparationInventoryProvider(books),
                shelfState,
                configs);

            service.StartOrResumeAsync(CancellationToken.None).GetAwaiter().GetResult();
            service.SetSelectedBookIdsAsync(new[] { "b3", "b1" }, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            service.ConfirmAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(service.CurrentState.UseExplicitSelectedBookIds);
            CollectionAssert.AreEqual(new[] { "b3", "b1" }, service.CurrentState.SelectedBookIds);
            CollectionAssert.AreEqual(new[] { "b3", "b1" }, shelfState.ShelfBookIds);
        }

        [Test]
        public void PreparationSession_ManualQuantity_ClearsExplicitSelection()
        {
            var books = new[]
            {
                SalesTestKit.Book("b1", genre: "Fact"),
                SalesTestKit.Book("b2", genre: "Travel"),
                SalesTestKit.Book("b3", genre: "Fantasy")
            };
            var configs = new FakeConfigsService();
            configs.SetAll(books);

            var service = new PreparationSessionService(
                new FakeSaveService(),
                new FakeDayProgressService(),
                new StaticPreparationInventoryProvider(books),
                new RecordingShelfStateService(),
                configs);

            service.StartOrResumeAsync(CancellationToken.None).GetAwaiter().GetResult();
            service.SetSelectedBookIdsAsync(new[] { "b1", "b2" }, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            service.SetGenreQuantityAsync("Fact", 1, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.IsFalse(service.CurrentState.UseExplicitSelectedBookIds);
        }

        [Test]
        public void PassiveMiss_RaisesCustomerPassivePurchaseFailed()
        {
            var c = Build(
                new BookConfig[0],
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                new List<Customer> { Passive("c1") });

            var failures = new List<string>();
            c.CustomerPassivePurchaseFailed += (customer, _) => failures.Add(customer.Id);

            StartDay(c);
            Run(c);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            CollectionAssert.AreEqual(new[] { "c1" }, failures);
            Assert.AreEqual(0, c.AccumulatedResult.SalesCount);
        }

        [Test]
        public void PassiveFailure_AbandonsRemainingPassiveSteps_AndLeaves()
        {
            // Empty shelf → every passive attempt misses. The first miss ends the passive chain, so the
            // second PassivePurchaseStep is dropped (one failure, not two) and only the closing tail runs.
            var c = Build(
                new BookConfig[0],
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(),
                new List<Customer>
                {
                    new("c1", new ICustomerStep[]
                    {
                        new ApproachStep(), new PassivePurchaseStep(), new PassivePurchaseStep(), new LeaveStep()
                    })
                });

            var failures = 0;
            c.CustomerPassivePurchaseFailed += (_, _) => failures++;

            StartDay(c);
            Run(c);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            Assert.AreEqual(1, failures, "First passive miss aborts the plan → the second passive never runs.");
            Assert.AreEqual(0, c.AccumulatedResult.SalesCount);
            Assert.AreEqual(1, c.AccumulatedResult.CustomersServed, "The aborting customer still leaves (served).");
        }

        [Test]
        public void PassiveFailure_BeforeActiveRequest_StillRunsTheMinigame()
        {
            // Plan: Approach → Passive(miss) → Active → Leave. Per ADR-0003 a passive miss ends only the
            // PASSIVE chain — the active step must still be entered, so the minigame opens.
            // The shelf must NOT be empty: ActiveRequestStep completes without opening the minigame when
            // there is nothing to recommend. So stock a book and force the miss with an always-miss gate.
            var req = SalesTestKit.ActiveRequest("reqA");
            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi") },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(),
                new List<Customer>
                {
                    new("c1", new ICustomerStep[]
                    {
                        new ApproachStep(), new PassivePurchaseStep(), new ActiveRequestStep(req), new LeaveStep()
                    })
                },
                passiveResolver: SalesTestKit.LegacyResolver(SalesTestKit.AlwaysMissPassiveSelector()));

            var activeStarted = 0;
            c.ActiveRequestStarted += _ => activeStarted++;
            var failures = 0;
            c.CustomerPassivePurchaseFailed += (_, _) => failures++;

            StartDay(c);
            DriveUntilActive(c);

            Assert.AreEqual(1, failures, "The passive attempt missed.");
            Assert.AreEqual(1, activeStarted, "The passive miss must not swallow the active step.");
            Assert.IsNotNull(c.CurrentRequest, "The minigame opened for the active request.");

            // The active step holds the interaction lock until the player resolves it; without resolving,
            // the day would never reach ReadyToClose.
            c.SkipCurrentRequest();
            Run(c);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
        }

        [Test]
        public void PassiveFailure_BeforeDialogue_StillRunsTheDialogue()
        {
            // Same ADR-0003 rule for the other non-passive step: a passive miss must not swallow a dialogue.
            var c = Build(
                new BookConfig[0],
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(),
                new List<Customer>
                {
                    new("c1", new ICustomerStep[]
                    {
                        new ApproachStep(), new PassivePurchaseStep(),
                        new DialogStep(new DialoguePayload("dlg")), new LeaveStep()
                    })
                });

            var dialogues = 0;
            c.DialogueStarted += (_, _) => dialogues++;

            StartDay(c);
            DriveUntilDialogue(c, () => dialogues);

            Assert.AreEqual(1, dialogues, "The passive miss must not swallow the dialogue step.");

            c.CompleteDialogue();
            Run(c);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
        }

        [Test]
        public void PassiveSuccess_DoesNotAbort_ContinuesToNextPassive()
        {
            // Guard against over-aborting: a successful passive returns plain Completed, so a second
            // passive step still runs. Two books so the day ends via "all customers done".
            var c = Build(
                new[]
                {
                    SalesTestKit.Book("b1", genre: "sci-fi", price: 80),
                    SalesTestKit.Book("b2", genre: "sci-fi", price: 80)
                },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                new List<Customer>
                {
                    new("c1", new ICustomerStep[]
                    {
                        new ApproachStep(), new PassivePurchaseStep(), new PassivePurchaseStep(), new LeaveStep()
                    })
                });

            var passive = 0;
            c.PassiveSaleHappened += _ => passive++;

            StartDay(c);
            Run(c);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            Assert.AreEqual(2, passive, "Both passive steps succeed; success does not end the cycle.");
            Assert.AreEqual(2, c.AccumulatedResult.SalesCount);
        }

        [Test]
        public void CompletePurchase_HappyPath_FiresWithPassiveCount()
        {
            // Three books so two passive sales leave one on the shelf — the day ends via "all customers
            // done" (not "all sold out"), letting the customer reach CompletePurchase with count 2.
            var c = Build(
                new[]
                {
                    SalesTestKit.Book("b1", genre: "sci-fi", price: 80),
                    SalesTestKit.Book("b2", genre: "sci-fi", price: 80),
                    SalesTestKit.Book("b3", genre: "sci-fi", price: 80)
                },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                new List<Customer>
                {
                    new("c1", new ICustomerStep[]
                    {
                        new ApproachStep(), new PassivePurchaseStep(), new PassivePurchaseStep(),
                        new CompletePurchaseStep(), new LeaveStep()
                    })
                });

            var completions = new List<int>();
            c.CustomerPurchaseCompleted += (_, count) => completions.Add(count);

            StartDay(c);
            Run(c);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            CollectionAssert.AreEqual(new[] { 2 }, completions, "Completion fires once with the passive count.");
        }

        [Test]
        public void CompletePurchase_AbortWithZeroSales_DoesNotFire()
        {
            // Empty shelf → first passive misses → abort → CompletePurchase skipped (count 0).
            var c = Build(
                new BookConfig[0],
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(),
                new List<Customer>
                {
                    new("c1", new ICustomerStep[]
                    {
                        new ApproachStep(), new PassivePurchaseStep(),
                        new CompletePurchaseStep(), new LeaveStep()
                    })
                });

            var completions = 0;
            c.CustomerPurchaseCompleted += (_, _) => completions++;

            StartDay(c);
            Run(c);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            Assert.AreEqual(0, completions, "No books bought → completion skipped.");
            Assert.AreEqual(1, c.AccumulatedResult.CustomersServed, "Customer still leaves (served).");
        }

        [Test]
        public void ActiveRequest_OnlyOneMinigame_PausesOthers_ThenSequencesFifo()
        {
            var reqA = SalesTestKit.ActiveRequest("reqA");
            var reqB = SalesTestKit.ActiveRequest("reqB");
            var c = Build(
                new[] { SalesTestKit.Book("b1") },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(),
                new List<Customer> { Active("c1", reqA), Active("c2", reqB) });

            var started = new List<string>();
            c.ActiveRequestStarted += r => started.Add(r.Id);

            StartDay(c);
            DriveUntilActive(c);

            Assert.AreEqual(1, started.Count, "Only one minigame opens.");
            Assert.AreEqual("reqA", c.CurrentRequest.Id);

            // Pause: while the lock is held, ticking makes no progress and no second minigame opens.
            for (var i = 0; i < 5; i++) c.Tick(0.1f);
            Assert.AreEqual(1, started.Count, "Second customer stays paused while the first is in the minigame.");

            c.SkipCurrentRequest();
            DriveUntilActive(c);

            Assert.AreEqual(2, started.Count);
            Assert.AreEqual("reqB", started[1], "FIFO: second request opens after the first resolves.");

            c.SkipCurrentRequest();
            Run(c);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            CollectionAssert.AreEqual(new[] { "reqA", "reqB" }, started);
            Assert.AreEqual(2, c.AccumulatedResult.SkippedCount);
        }

        [Test]
        public void ReserveContention_TwoPassive_PickDifferentBooks()
        {
            // With the probabilistic selector always firing, each passive customer reserves an
            // available book; the reservation hides it from the second customer's pick.
            var c = Build(
                new[]
                {
                    SalesTestKit.Book("b1", genre: "sci-fi"),
                    SalesTestKit.Book("b2", genre: "romance", qualities: new[] { "summer" })
                },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                new List<Customer> { Passive("c1"), Passive("c2") });

            var soldIds = new List<string>();
            c.PassiveSaleHappened += e => soldIds.Add(e.BookId);

            StartDay(c);
            Run(c);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            Assert.AreEqual(2, soldIds.Count, "Both customers buy.");
            CollectionAssert.AreEquivalent(new[] { "b1", "b2" }, soldIds, "No double-reservation: customers pick distinct books.");
            Assert.IsTrue(c.Shelf.AllSoldOut());
        }

        [Test]
        public void SoldOut_StopsSpawning_FinishesInFlight_ThenReadyToClose()
        {
            // Single book, three customers, spawned one-at-a-time (large SpawnInterval). c1 spawns and
            // buys the only book; once sold out, spawning stops so c2/c3 never appear. c1 must still run
            // its closing steps (LeaveStep → Done) before the day is closable — AllSoldOut no longer ends
            // the day instantly.
            var tuning = SalesTestKit.FastTuning();
            tuning.SpawnInterval = 100f;   // only the first customer spawns within the test's tick budget

            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi") },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                new List<Customer> { Passive("c1"), Passive("c2"), Passive("c3") },
                tuning);

            StartDay(c);
            Run(c);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase, "Day waits for the in-flight buyer to finish, then is closable.");
            Assert.AreEqual(1, c.AccumulatedResult.SalesCount, "Only one book to sell.");
            Assert.IsTrue(c.Shelf.AllSoldOut());
            Assert.AreEqual(1, c.AccumulatedResult.CustomersServed,
                "Only c1 was served; spawning stopped on sold-out so c2/c3 never appeared.");
        }

        [Test]
        public void LastBookBought_RunsClosingSteps_ThenReadyToClose()
        {
            // Repro of the freeze bug: 1 book, 1 customer with the full closing tail. Buying the last
            // book must NOT end the day before CompletePurchase + Leave run.
            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi", price: 70) },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                new List<Customer>
                {
                    new("c1", new ICustomerStep[]
                    {
                        new ApproachStep(), new PassivePurchaseStep(),
                        new CompletePurchaseStep(), new LeaveStep()
                    })
                });

            var completions = new List<int>();
            c.CustomerPurchaseCompleted += (_, count) => completions.Add(count);
            SalesDayResult published = null;
            c.DayCompleted += r => published = r;

            StartDay(c);
            Run(c);

            // Day did NOT auto-complete; the customer finished its plan.
            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            Assert.IsTrue(c.Shelf.AllSoldOut());
            CollectionAssert.AreEqual(new[] { 1 }, completions, "CompletePurchase ran for the one bought book.");
            Assert.AreEqual(1, c.AccumulatedResult.CustomersServed, "Buyer reached Done, not frozen.");
            Assert.IsNull(published, "Results not published until the player closes the shop.");

            c.ConcludeDay();

            Assert.IsTrue(c.IsDayCompleted);
            Assert.IsNotNull(published);
            Assert.AreEqual(1, published.SalesCount);
            Assert.AreEqual(1, published.CustomersServed);
        }

        [Test]
        public void RecommendBook_ActiveMinigame_ScoresSellsAndCompletes()
        {
            var reqA = SalesTestKit.ActiveRequest("reqA");
            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi", price: 80) },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(),
                new List<Customer> { Active("c1", reqA) });

            RecommendationResult resolved = null;
            c.RecommendationResolved += r => resolved = r;

            StartDay(c);
            DriveUntilActive(c);
            Assert.AreEqual("reqA", c.CurrentRequest.Id);

            c.RecommendBook("b1");

            Assert.IsNotNull(resolved);
            Assert.AreEqual(RecommendationTier.Excellent, resolved.Tier);
            Assert.AreEqual("b1", resolved.BookId);
            Assert.AreEqual(ShelfBookState.SoldOut, c.Shelf.Find("b1").State);
            Assert.AreEqual(BookConfig.FixedPriceGold, c.AccumulatedResult.GoldEarned);

            Run(c);
            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
        }

        [Test]
        public void RecommendBook_ConditionMatch_ReturnsExcellentAndFixedGold()
        {
            var request = ConditionRequest("req_condition", "Detective");
            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "Crime", qualities: new[] { "Detective" }) },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(),
                new List<Customer> { Active("c1", request) });

            RecommendationResult resolved = null;
            c.RecommendationResolved += r => resolved = r;

            StartDay(c);
            DriveUntilActive(c);
            c.RecommendBook("b1");

            Assert.IsNotNull(resolved);
            Assert.AreEqual(RecommendationTier.Excellent, resolved.Tier);
            Assert.AreEqual(BookConfig.FixedPriceGold, resolved.GoldEarned);
            Assert.IsNull(resolved.SoldGenre);
            Assert.AreEqual(BookConfig.FixedPriceGold, c.AccumulatedResult.GoldEarned);
            Assert.AreEqual(ShelfBookState.SoldOut, c.Shelf.Find("b1").State);
        }

        [Test]
        public void RecommendBook_ActiveGenreMatch_AttributesSaleToMatchedSecondaryGenre()
        {
            var request = GenreRequest("req_kids", "Kids");
            var c = Build(
                new[] { SalesTestKit.Book("b1", genres: new[] { "Classic", "Kids" }) },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(),
                new List<Customer> { Active("c1", request) });

            RecommendationResult resolved = null;
            c.RecommendationResolved += r => resolved = r;

            StartDay(c);
            DriveUntilActive(c);
            c.RecommendBook("b1");

            Assert.IsNotNull(resolved);
            Assert.AreEqual(RecommendationTier.Excellent, resolved.Tier);
            Assert.AreEqual("Kids", resolved.SoldGenre);
        }

        [Test]
        public void RecommendBook_MultipleActiveGenreMatches_UsesRequestOrder()
        {
            var request = GenreRequest("req_kids_classic", "Kids", "Classic");
            var c = Build(
                new[] { SalesTestKit.Book("b1", genres: new[] { "Classic", "Kids" }) },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(),
                new List<Customer> { Active("c1", request) });

            RecommendationResult resolved = null;
            c.RecommendationResolved += r => resolved = r;

            StartDay(c);
            DriveUntilActive(c);
            c.RecommendBook("b1");

            Assert.IsNotNull(resolved);
            Assert.AreEqual(RecommendationTier.Excellent, resolved.Tier);
            Assert.AreEqual("Kids", resolved.SoldGenre);
        }

        [Test]
        public void RecommendBook_ConditionMismatch_ReturnsFailedAndDoesNotSell()
        {
            var request = ConditionRequest("req_condition", "Detective");
            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "Crime", qualities: new[] { "Romance" }) },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(),
                new List<Customer> { Active("c1", request) });

            RecommendationResult resolved = null;
            c.RecommendationResolved += r => resolved = r;

            StartDay(c);
            DriveUntilActive(c);
            c.RecommendBook("b1");

            Assert.IsNotNull(resolved);
            Assert.AreEqual(RecommendationTier.Failed, resolved.Tier);
            Assert.AreEqual(0, resolved.GoldEarned);
            Assert.AreEqual(0, c.AccumulatedResult.GoldEarned);
            Assert.AreEqual(ShelfBookState.Available, c.Shelf.Find("b1").State);
        }

        [Test]
        public void RecommendBook_WithNoActiveMinigame_IsIgnored()
        {
            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi") },
                Array.Empty<RequestDefinitionConfig>(),
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                new List<Customer> { Passive("c1") });

            StartDay(c);
            // No active minigame is open yet → recommend is a no-op (no exception, no sale via this path).
            c.RecommendBook("b1");

            Assert.AreEqual(0, c.AccumulatedResult.ManualRequests);
        }
    }
}
