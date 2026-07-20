using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Book.Sell.API;
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
        public async System.Threading.Tasks.Task EnterAsync_ClearsDayOneAuthoredDialogueBeforeEnteringLocation()
        {
            var events = new List<string>();
            var configs = ConfigsWith(
                locations: new[] { new LocationConfig { Id = "loc" } },
                books: FirstDayBooks(),
                scripts: new[]
                {
                    new CustomerScriptConfig { Id = "eddi_intro", ActivationQuestId = "q_intro_eddi", DialogueId = "eddy1" },
                    new CustomerScriptConfig { Id = "future_day", DayIndex = 2, DialogueId = "future" },
                    new CustomerScriptConfig { Id = "no_dialogue", DayIndex = 1 }
                });
            var delivered = new RecordingDeliveredDialogues(events);
            var gameFlow = new RecordingGameFlow(events);
            var flow = new FirstDayEntryFlow(
                new FakeMorning(),
                new FakePreparation(),
                gameFlow,
                configs,
                inventory: new FakeInventory(FirstDayBooks()),
                delivered: delivered);

            var entered = await flow.EnterAsync(CancellationToken.None);

            Assert.IsTrue(entered);
            CollectionAssert.AreEqual(new[] { "eddy1" }, delivered.Cleared);
            Assert.Less(events.IndexOf("clear:eddy1"), events.IndexOf("enter:loc"));
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
                new BookConfig { Id = "fact", Genres = new[] { "Fact" }, RarityWeight = 1f },
                new BookConfig { Id = "travel", Genres = new[] { "Travel" }, RarityWeight = 1f }
            };

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
            public PreparationCapacity Capacity { get; } = new PreparationCapacity(0, 2);
            public PreparationSessionState CurrentState { get; private set; }
            public event Action<PreparationSessionState> StateChanged { add { } remove { } }
            public int TotalSelected { get; private set; }

            public UniTask<IReadOnlyList<GenreSelectionItem>> StartOrResumeAsync(CancellationToken ct, string locationId = null)
                => UniTask.FromResult<IReadOnlyList<GenreSelectionItem>>(Array.Empty<GenreSelectionItem>());

            public UniTask<IReadOnlyDictionary<string, int>> GetGenreQuantitiesPreviewAsync(CancellationToken ct)
                => UniTask.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>());

            public UniTask SetGenreQuantityAsync(string genre, int quantity, CancellationToken ct)
                => UniTask.CompletedTask;

            public UniTask SetSelectedBookIdsAsync(IReadOnlyList<string> bookIds, CancellationToken ct)
            {
                TotalSelected = bookIds?.Count ?? 0;
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

        private sealed class RecordingDeliveredDialogues : IDeliveredDialoguesService
        {
            private readonly List<string> _events;
            public RecordingDeliveredDialogues(List<string> events) => _events = events;
            public List<string> Cleared { get; } = new();
            public bool IsDelivered(string dialogueId) => false;
            public UniTask MarkDeliveredAsync(string dialogueId, CancellationToken ct) => UniTask.CompletedTask;

            public UniTask ClearAsync(string dialogueId, CancellationToken ct)
            {
                Cleared.Add(dialogueId);
                _events.Add($"clear:{dialogueId}");
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
