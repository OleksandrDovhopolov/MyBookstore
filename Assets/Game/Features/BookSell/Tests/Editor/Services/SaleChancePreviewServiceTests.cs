using System;
using System.Collections.Generic;
using System.Threading;
using Book.Sell.API;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Game.DayCycle.Day;
using Game.Decor;
using Game.Preparation.Domain;
using Game.Preparation.Services;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor.Services
{
    public sealed class SaleChancePreviewServiceTests
    {
        [Test]
        public void CountZero_ReturnsZeroPercent()
        {
            var service = BuildService(count: 0);

            var percent = service.GetPercentAsync(BookGenre.Fantasy, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(0, percent);
        }

        [Test]
        public void CountOne_NonDemandLocation_ReturnsBasePlusPerCopyPercent()
        {
            var service = BuildService(count: 1, demandGenres: new[] { BookGenre.Crime.ToString() });

            var percent = service.GetPercentAsync(BookGenre.Fantasy, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(10, percent);
        }

        [Test]
        public void CountOne_DemandLocation_AppliesLocationMultiplier()
        {
            var service = BuildService(count: 1, demandGenres: new[] { BookGenre.Fantasy.ToString() });

            var percent = service.GetPercentAsync(BookGenre.Fantasy, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(15, percent);
        }

        [Test]
        public void ActiveDecorIds_ArePassedToCalculator()
        {
            var service = BuildService(
                count: 1,
                demandGenres: new[] { BookGenre.Crime.ToString() },
                activeDecorIds: new[] { "decor_bonus" },
                decorMultiplier: 2f);

            var percent = service.GetPercentAsync(BookGenre.Fantasy, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(20, percent);
        }

        [Test]
        public void MissingLocationConfig_IsTreatedAsNoDemandLocation()
        {
            var service = BuildService(count: 1, locationId: "missing_location", includeLocation: false);

            var percent = service.GetPercentAsync(BookGenre.Fantasy, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(10, percent);
        }

        private static SaleChancePreviewService BuildService(
            int count,
            string locationId = "loc",
            bool includeLocation = true,
            string[] demandGenres = null,
            string[] activeDecorIds = null,
            float decorMultiplier = 1f)
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[]
            {
                new EconomyConfig
                {
                    Id = EconomyConfig.SingletonId,
                    BaseSaleChance = 0.05,
                    PerCopyChance = 0.05,
                    CapChance = 0.5,
                    LocationDemandMultiplier = 1.5
                }
            });

            if (includeLocation)
            {
                configs.SetAll(new[]
                {
                    new LocationConfig
                    {
                        Id = locationId,
                        DisplayNameKey = locationId,
                        DemandGenres = demandGenres
                    }
                });
            }

            var session = new FakePreparationSession(locationId, new Dictionary<string, int>
            {
                [BookGenre.Fantasy.ToString()] = count
            });
            var decorPlacement = new FakeDecorPlacementService(activeDecorIds ?? Array.Empty<string>());
            var calculator = new EconomyBasedSaleChanceCalculator(
                configs,
                new ActiveDecorMultiplierProvider("decor_bonus", decorMultiplier));

            return new SaleChancePreviewService(
                session,
                configs,
                decorPlacement,
                calculator,
                new FakeSaveService(),
                new FakeDayProgressService());
        }

        private sealed class FakePreparationSession : IPreparationSessionService
        {
            private readonly IReadOnlyDictionary<string, int> _counts;

            public FakePreparationSession(string locationId, IReadOnlyDictionary<string, int> counts)
            {
                var genreQuantities = new Dictionary<string, int>();
                foreach (var kv in counts)
                    genreQuantities[kv.Key] = kv.Value;

                CurrentState = new PreparationSessionState
                {
                    LocationId = locationId,
                    GenreQuantities = genreQuantities
                };
                _counts = counts;
            }

            public PreparationCapacity Capacity { get; } = new(0, 12);
            public PreparationSessionState CurrentState { get; }
            public event Action<PreparationSessionState> StateChanged;
            public int TotalSelected => 0;
            public UniTask<IReadOnlyList<GenreSelectionItem>> StartOrResumeAsync(CancellationToken ct, string locationId = null)
                => UniTask.FromResult<IReadOnlyList<GenreSelectionItem>>(Array.Empty<GenreSelectionItem>());
            public UniTask<IReadOnlyDictionary<string, int>> GetGenreQuantitiesPreviewAsync(CancellationToken ct)
                => UniTask.FromResult(_counts);
            public UniTask SetGenreQuantityAsync(string genre, int quantity, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SetSelectedBookIdsAsync(IReadOnlyList<string> bookIds, CancellationToken ct) => UniTask.CompletedTask;
            public PreparationValidationResult Validate() => PreparationValidationResult.Ok();
            public UniTask<bool> ConfirmAsync(CancellationToken ct) => UniTask.FromResult(true);
            public UniTask RestoreAfterEntryFailureAsync(string locationId, CancellationToken ct) => UniTask.CompletedTask;

            public void RaiseStateChanged() => StateChanged?.Invoke(CurrentState);
        }

        private sealed class FakeDayProgressService : IDayProgressService
        {
            public event Action<DayProgressState> PhaseChanged;
            public DayProgressState Current { get; } = new();
            public UniTask<DayProgressState> LoadAsync(CancellationToken ct) => UniTask.FromResult(Current);
            public UniTask SetPhaseAsync(DayPhase phase, CancellationToken ct)
            {
                Current.CurrentPhase = phase;
                PhaseChanged?.Invoke(Current);
                return UniTask.CompletedTask;
            }

            public UniTask MarkCurrentDayCompletedAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask AdvanceToNextDayAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SaveAsync(CancellationToken ct) => UniTask.CompletedTask;
        }

        private sealed class FakeDecorPlacementService : IDecorPlacementService
        {
            private readonly IReadOnlyList<string> _activeDecorIds;

            public FakeDecorPlacementService(IReadOnlyList<string> activeDecorIds)
            {
                _activeDecorIds = activeDecorIds;
            }

            public event Action PlacementChanged;
            public event Action<DecorPlacementChange> PlacementActionPerformed { add { } remove { } }
            public IReadOnlyList<DecorPlacementEntry> GetAllPlacements() => Array.Empty<DecorPlacementEntry>();
            public string GetDecorInSlot(string slotId) => null;
            public IReadOnlyList<string> GetActiveDecorIds() => _activeDecorIds;
            public UniTask<DecorPlacementResult> PlaceAsync(string decorId, string slotId, CancellationToken ct) => UniTask.FromResult(DecorPlacementResult.Success);
            public UniTask<DecorPlacementResult> ReplaceAsync(string decorId, string slotId, CancellationToken ct) => UniTask.FromResult(DecorPlacementResult.Success);
            public UniTask UnplaceAsync(string slotId, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask ClearAllAsync(CancellationToken ct) => UniTask.CompletedTask;

            public void RaisePlacementChanged() => PlacementChanged?.Invoke();
        }

        private sealed class ActiveDecorMultiplierProvider : IDecorModifierProvider
        {
            private readonly string _decorId;
            private readonly float _multiplier;

            public ActiveDecorMultiplierProvider(string decorId, float multiplier)
            {
                _decorId = decorId;
                _multiplier = multiplier;
            }

            public float GetGenreMultiplier(string genre, IReadOnlyList<string> activeDecorIds)
            {
                if (activeDecorIds == null) return 1f;
                for (var i = 0; i < activeDecorIds.Count; i++)
                {
                    if (activeDecorIds[i] == _decorId)
                        return _multiplier;
                }

                return 1f;
            }
        }
    }
}
