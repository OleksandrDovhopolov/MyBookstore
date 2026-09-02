using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
using Game.Configs;
using Game.Configs.Models;
using Game.DayCycle.Day;
using Game.DayCycle.Morning;
using Game.DayCycle.Morning.Model;
using Game.Ftue.Services;
using Game.LocationUnlock.API;
using Game.Preparation.Domain;
using Game.Preparation.Services;
using NUnit.Framework;
using UnityEngine;

namespace GameplayUI.Tests.Editor
{
    public sealed class FirstDayEntryFlowTests
    {
        [Test]
        public async System.Threading.Tasks.Task EnterAsync_AutoStocksAndEntersLocation()
        {
            var events = new List<string>();
            var preparation = new FakePreparation();
            var flow = new FirstDayEntryFlow(
                new FakeMorning(),
                preparation,
                new RecordingGameFlow(events),
                ConfigsWith(new[] { new LocationConfig { Id = "loc" } }, FirstDayBooks(), Array.Empty<CustomerScriptConfig>()),
                inventory: new FakeInventory(FirstDayBooks()));

            var entered = await flow.EnterAsync(CancellationToken.None);

            Assert.IsTrue(entered);
            Assert.AreEqual(2, preparation.TotalSelected);
            CollectionAssert.Contains(events, "enter:loc");
        }

        [Test]
        public async System.Threading.Tasks.Task EnterAsync_AutoStocksGenresFromDayOnePassiveAttempts()
        {
            var scripts = new[]
            {
                Script("eddi_intro", 1,
                    Attempt("Fact", forceHit: true),
                    Attempt("Travel", forceHit: false))
            };
            var books = FirstDayBooks();
            var preparation = new FakePreparation(slots: 2);
            var flow = new FirstDayEntryFlow(
                new FakeMorning(),
                preparation,
                new RecordingGameFlow(new List<string>()),
                ConfigsWith(new[] { new LocationConfig { Id = "loc" } }, books, scripts),
                inventory: new FakeInventory(books));

            var entered = await flow.EnterAsync(CancellationToken.None);

            Assert.IsTrue(entered);
            var expectedGenres = CustomerScriptDayLookup.PassiveGenresForDay(scripts, 1);
            var selectedBooks = books.Where(b => preparation.SelectedBookIds.Contains(b.Id)).ToArray();
            foreach (var genre in expectedGenres)
            {
                Assert.IsTrue(
                    selectedBooks.Any(b => string.Equals(b.PrimaryGenre, genre, StringComparison.OrdinalIgnoreCase)),
                    $"Day-1 shelf must include a book for scripted passive genre '{genre}'.");
            }
        }

        [Test]
        public async System.Threading.Tasks.Task EnterAsync_WhenDayOneScriptGenreChanges_ReservesThatGenre()
        {
            var scripts = new[] { Script("custom_intro", 1, Attempt("Crime", forceHit: true)) };
            var books = new[]
            {
                Book("fact", "Fact"),
                Book("travel", "Travel"),
                Book("crime", "Crime")
            };
            var preparation = new FakePreparation(slots: 1);
            var flow = new FirstDayEntryFlow(
                new FakeMorning(),
                preparation,
                new RecordingGameFlow(new List<string>()),
                ConfigsWith(new[] { new LocationConfig { Id = "loc" } }, books, scripts),
                inventory: new FakeInventory(books));

            var entered = await flow.EnterAsync(CancellationToken.None);

            Assert.IsTrue(entered);
            CollectionAssert.AreEqual(new[] { "crime" }, preparation.SelectedBookIds);
        }

        [Test]
        public async System.Threading.Tasks.Task EnterAsync_ScriptWithoutPassiveAttempts_DoesNotFail()
        {
            var scripts = new[] { Script("dialogue_only", 1, attempts: null) };
            var books = FirstDayBooks();
            var preparation = new FakePreparation(slots: 2);
            var flow = new FirstDayEntryFlow(
                new FakeMorning(),
                preparation,
                new RecordingGameFlow(new List<string>()),
                ConfigsWith(new[] { new LocationConfig { Id = "loc" } }, books, scripts),
                inventory: new FakeInventory(books));

            var entered = await flow.EnterAsync(CancellationToken.None);

            Assert.IsTrue(entered);
            Assert.AreEqual(2, preparation.TotalSelected);
        }

        [Test]
        public async System.Threading.Tasks.Task EnterAsync_WhenScriptedGenresExceedSlots_ClampsToCapacity()
        {
            var scripts = new[]
            {
                Script("crowded_day", 1,
                    Attempt("Fact", forceHit: true),
                    Attempt("Travel", forceHit: false),
                    Attempt("Crime", forceHit: true))
            };
            var books = new[]
            {
                Book("fact", "Fact"),
                Book("travel", "Travel"),
                Book("crime", "Crime")
            };
            var preparation = new FakePreparation(slots: 2);
            var flow = new FirstDayEntryFlow(
                new FakeMorning(),
                preparation,
                new RecordingGameFlow(new List<string>()),
                ConfigsWith(new[] { new LocationConfig { Id = "loc" } }, books, scripts),
                inventory: new FakeInventory(books));

            var entered = await flow.EnterAsync(CancellationToken.None);

            Assert.IsTrue(entered);
            Assert.AreEqual(preparation.Capacity.DailyBookSlots, preparation.TotalSelected);
        }

        [Test]
        public async System.Threading.Tasks.Task DirectEntryGate_AllowsUnfinishedDayOneInSalesPhase()
        {
            var bootstrap = CreateBootstrap(new DayProgressState
            {
                CurrentDay = 1,
                CurrentPhase = DayPhase.Sales
            });

            Assert.IsTrue(await InvokeShouldEnterFirstDayLocationAsync(bootstrap));
        }

        [Test]
        public async System.Threading.Tasks.Task DirectEntryGate_BlocksCompletedDayOne()
        {
            var bootstrap = CreateBootstrap(new DayProgressState
            {
                CurrentDay = 1,
                CurrentPhase = DayPhase.Sales,
                CompletedDays = new List<int> { 1 }
            });

            Assert.IsFalse(await InvokeShouldEnterFirstDayLocationAsync(bootstrap));
        }

        private static MainSceneBootstrap CreateBootstrap(DayProgressState state)
        {
            var go = new GameObject("MainSceneBootstrapTests");
            var bootstrap = go.AddComponent<MainSceneBootstrap>();
            bootstrap.Install(
                null,
                null,
                null,
                null,
                new WelcomeWindowStartupSettings(false),
                new FirstDayEntrySettings(FirstDayEntryMode.Location),
                new FakeDayProgress(state),
                new FakeMorning(),
                new FakePreparation(),
                new RecordingGameFlow(new List<string>()),
                ConfigsWith(new[] { new LocationConfig { Id = "loc" } }, FirstDayBooks(), Array.Empty<CustomerScriptConfig>()),
                null,
                new FakeInventory(FirstDayBooks()));
            return bootstrap;
        }

        private static async UniTask<bool> InvokeShouldEnterFirstDayLocationAsync(MainSceneBootstrap bootstrap)
        {
            try
            {
                var method = typeof(MainSceneBootstrap).GetMethod(
                    "ShouldEnterFirstDayLocationAsync",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var task = (UniTask<bool>)method.Invoke(bootstrap, new object[] { CancellationToken.None });
                return await task;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bootstrap.gameObject);
            }
        }

        private static BookConfig[] FirstDayBooks()
            => new[]
            {
                Book("fact", "Fact"),
                Book("travel", "Travel")
            };

        private static BookConfig Book(string id, string genre)
            => new() { Id = id, Genres = new[] { genre }, RarityWeight = 1f };

        private static CustomerScriptConfig Script(
            string id,
            int day,
            params ScriptedPassivePurchaseConfig[] attempts)
            => new()
            {
                Id = id,
                DayIndex = day,
                PassiveAttempts = attempts
            };

        private static ScriptedPassivePurchaseConfig Attempt(string genre, bool forceHit)
            => new() { Genre = genre, ForceHit = forceHit };

        private static FakeConfigsService ConfigsWith(
            IReadOnlyList<LocationConfig> locations,
            IReadOnlyList<BookConfig> books,
            IReadOnlyList<CustomerScriptConfig> scripts)
        {
            var configs = new FakeConfigsService();
            configs.SetAll(locations);
            configs.SetAll(books);
            configs.SetAll(scripts);
            return configs;
        }

        private sealed class FakeMorning : IMorningSessionService
        {
            public MorningDayContext CurrentContext { get; private set; }

            public UniTask<MorningDayContext> StartOrResumeAsync(CancellationToken ct)
            {
                CurrentContext = new MorningDayContext { Day = 1, DayId = "day_1" };
                return UniTask.FromResult(CurrentContext);
            }

            public UniTask<MorningContinueResult> ContinueToPreparationAsync(CancellationToken ct)
                => UniTask.FromResult(new MorningContinueResult { Day = 1, DayId = "day_1" });
        }

        private sealed class FakePreparation : IPreparationSessionService
        {
            public FakePreparation(int slots = 2)
                => Capacity = new PreparationCapacity(0, slots);

            public PreparationCapacity Capacity { get; }
            public PreparationSessionState CurrentState { get; private set; }
            public event Action<PreparationSessionState> StateChanged { add { } remove { } }
            public int TotalSelected { get; private set; }
            public IReadOnlyList<string> SelectedBookIds { get; private set; } = Array.Empty<string>();

            public UniTask<IReadOnlyList<GenreSelectionItem>> StartOrResumeAsync(CancellationToken ct, string locationId = null)
                => UniTask.FromResult<IReadOnlyList<GenreSelectionItem>>(Array.Empty<GenreSelectionItem>());

            public UniTask<IReadOnlyDictionary<string, int>> GetGenreQuantitiesPreviewAsync(CancellationToken ct)
                => UniTask.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>());

            public UniTask SetGenreQuantityAsync(string genre, int quantity, CancellationToken ct)
                => UniTask.CompletedTask;

            public UniTask SetSelectedBookIdsAsync(IReadOnlyList<string> bookIds, CancellationToken ct)
            {
                SelectedBookIds = bookIds?.ToArray() ?? Array.Empty<string>();
                TotalSelected = SelectedBookIds.Count;
                return UniTask.CompletedTask;
            }

            public PreparationValidationResult Validate() => PreparationValidationResult.Ok();
            public UniTask<bool> ConfirmAsync(CancellationToken ct) => UniTask.FromResult(true);
            public UniTask RestoreAfterEntryFailureAsync(string locationId, CancellationToken ct) => UniTask.CompletedTask;
        }

        private sealed class FakeInventory : IPreparationInventoryProvider
        {
            private readonly IReadOnlyList<BookConfig> _books;
            public FakeInventory(IReadOnlyList<BookConfig> books) => _books = books;
            public IReadOnlyList<BookConfig> GetOwnedBooks() => _books;
        }

        private sealed class RecordingGameFlow : IGameFlowService
        {
            private readonly List<string> _events;
            public RecordingGameFlow(List<string> events) => _events = events;
            public bool IsTransitioning { get; private set; }
            public bool IsLocationLoaded { get; private set; }
            public event Action<bool> LocationLoadedChanged;
            public void RegisterHubRoot(GameObject hubRoot) { }

            public UniTask EnterLocationAsync(string locationId, CancellationToken ct = default)
            {
                _events.Add($"enter:{locationId}");
                IsLocationLoaded = true;
                LocationLoadedChanged?.Invoke(true);
                return UniTask.CompletedTask;
            }

            public UniTask ReturnToHubAsync(CancellationToken ct = default)
            {
                IsLocationLoaded = false;
                LocationLoadedChanged?.Invoke(false);
                return UniTask.CompletedTask;
            }
        }

        private sealed class FakeDayProgress : IDayProgressService
        {
            public FakeDayProgress(DayProgressState state) => Current = state;
            public event Action<DayProgressState> PhaseChanged { add { } remove { } }
            public DayProgressState Current { get; }
            public UniTask<DayProgressState> LoadAsync(CancellationToken ct) => UniTask.FromResult(Current);
            public UniTask SetPhaseAsync(DayPhase phase, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask MarkCurrentDayCompletedAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask AdvanceToNextDayAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SaveAsync(CancellationToken ct) => UniTask.CompletedTask;
        }

        private sealed class FakeConfigsService : IConfigsService
        {
            private readonly Dictionary<Type, IReadOnlyList<IConfig>> _byType = new();

            public void SetAll<T>(IReadOnlyList<T> items) where T : class, IConfig
                => _byType[typeof(T)] = items.Cast<IConfig>().ToList();

            public IReadOnlyList<T> GetAll<T>() where T : class, IConfig
                => _byType.TryGetValue(typeof(T), out var list) ? list.Cast<T>().ToList() : Array.Empty<T>();

            public T Get<T>(string id) where T : class, IConfig
                => GetAll<T>().FirstOrDefault(c => c.Id == id);

            public bool TryGet<T>(string id, out T config) where T : class, IConfig
            {
                config = Get<T>(id);
                return config != null;
            }

            public UniTask<T> GetAsync<T>(string id) where T : class, IConfig
                => UniTask.FromResult(Get<T>(id));

            public bool IsExists<T>(string id) where T : class, IConfig
                => Get<T>(id) != null;

            public UniTask WarmupAsync(CancellationToken ct) => UniTask.CompletedTask;
        }
    }
}
