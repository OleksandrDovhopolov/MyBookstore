using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Game.DayCycle.Day;
using Game.DayCycle.Morning;
using Game.DayCycle.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Game.DayCycle.Tests.Editor
{
    public sealed class MorningSessionServiceTests
    {
        private static FakeConfigsService Configs()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[]
            {
                new DayConfig
                {
                    Id = "day_001", DayIndex = 1, Title = "День 1",
                    WeatherId = "clear", EventId = "exam_week",
                    DemandGenres = new[] { "science" }, TargetLocationIds = new[] { "loc_downtown" }
                },
                new DayConfig
                {
                    Id = "day_002", DayIndex = 2, Title = "День 2",
                    WeatherId = "rain", EventId = ""
                }
            });
            return configs;
        }

        private static MorningSessionService Service(FakeSaveService save, FakeConfigsService configs)
        {
            var progress = new DayProgressService(save);
            var resolver = new MorningContextResolver(configs);
            return new MorningSessionService(progress, resolver);
        }

        // EditMode-helper: фейки завершаются синхронно, поэтому безопасно разворачиваем UniTask.
        private static T Run<T>(UniTask<T> task) => task.GetAwaiter().GetResult();
        private static void Run(UniTask task) => task.GetAwaiter().GetResult();

        [Test]
        public void StartOrResume_FreshState_ResolvesDayOneContext()
        {
            var save = new FakeSaveService();
            var ctx = Run(Service(save, Configs()).StartOrResumeAsync(CancellationToken.None));

            Assert.AreEqual(1, ctx.Day);
            Assert.AreEqual("day_001", ctx.DayId);
        }

        [Test]
        public void StartOrResume_PersistedDayTwo_ResolvesDayTwoContext()
        {
            var save = new FakeSaveService();
            // Имитируем сохранённый прогресс: день 2, уже на фазе утра.
            Run(new DayProgressService(save).SetPhaseAsync(DayPhase.Morning, CancellationToken.None));
            var progress = new DayProgressService(save);
            Run(progress.LoadAsync(CancellationToken.None));
            progress.Current.CurrentDay = 2;
            Run(progress.SaveAsync(CancellationToken.None));

            var ctx = Run(Service(save, Configs()).StartOrResumeAsync(CancellationToken.None));

            Assert.AreEqual(2, ctx.Day);
            Assert.AreEqual("day_002", ctx.DayId);
            Assert.AreEqual("rain", ctx.WeatherId);
        }

        [Test]
        public void StartOrResume_PhaseNotMorning_ResetsPhaseToMorningAndSaves()
        {
            var save = new FakeSaveService();
            var progress = new DayProgressService(save);
            Run(progress.LoadAsync(CancellationToken.None));
            progress.Current.CurrentPhase = DayPhase.Sales;
            Run(progress.SaveAsync(CancellationToken.None));

            Run(Service(save, Configs()).StartOrResumeAsync(CancellationToken.None));

            var reloaded = new DayProgressService(save);
            Run(reloaded.LoadAsync(CancellationToken.None));
            Assert.AreEqual(DayPhase.Morning, reloaded.Current.CurrentPhase);
        }

        [Test]
        public void Continue_SetsPhasePreparation_AndIsPersistedAcrossRestart()
        {
            var save = new FakeSaveService();
            var service = Service(save, Configs());
            Run(service.StartOrResumeAsync(CancellationToken.None));

            var result = Run(service.ContinueToPreparationAsync(CancellationToken.None));

            Assert.AreEqual(1, result.Day);
            CollectionAssert.AreEqual(new[] { "weather_clear", "event_exam_week" }, result.ActiveModifierIds);
            CollectionAssert.AreEqual(new[] { "loc_downtown" }, result.TargetLocationIds);

            // «Перезапуск»: новый сервис над тем же хранилищем видит фазу Preparation.
            var afterRestart = new DayProgressService(new FakeSaveService(save.Store));
            Run(afterRestart.LoadAsync(CancellationToken.None));
            Assert.AreEqual(DayPhase.Preparation, afterRestart.Current.CurrentPhase);
        }

        [Test]
        public void Continue_WithoutExplicitStart_StillResolvesAndContinues()
        {
            var save = new FakeSaveService();
            var result = Run(Service(save, Configs()).ContinueToPreparationAsync(CancellationToken.None));

            Assert.AreEqual(1, result.Day);
            Assert.AreEqual("day_001", result.DayId);
        }

        [Test]
        public void Continue_WhenCurrentDayCompleted_ReturnsNull_AndKeepsResultsPhase()
        {
            var save = new FakeSaveService();
            var progress = new DayProgressService(save);
            Run(progress.LoadAsync(CancellationToken.None));
            progress.Current.CompletedDays.Add(progress.Current.CurrentDay);
            progress.Current.CurrentPhase = DayPhase.Morning;
            Run(progress.SaveAsync(CancellationToken.None));

            var result = Run(Service(save, Configs()).ContinueToPreparationAsync(CancellationToken.None));

            Assert.IsNull(result);

            var reloaded = new DayProgressService(new FakeSaveService(save.Store));
            Run(reloaded.LoadAsync(CancellationToken.None));
            Assert.AreEqual(DayPhase.Results, reloaded.Current.CurrentPhase);
            CollectionAssert.Contains(reloaded.Current.CompletedDays, 1);
        }

        [Test]
        public void Continue_WhenCurrentDayCompletedAndAlreadyResults_ReemitsResultsPhase()
        {
            var save = new FakeSaveService();
            var progress = new DayProgressService(save);
            Run(progress.LoadAsync(CancellationToken.None));
            progress.Current.CompletedDays.Add(progress.Current.CurrentDay);
            progress.Current.CurrentPhase = DayPhase.Results;
            Run(progress.SaveAsync(CancellationToken.None));

            var phaseChangedCount = 0;
            DayProgressState emitted = null;
            progress.PhaseChanged += state =>
            {
                phaseChangedCount++;
                emitted = state;
            };

            var service = new MorningSessionService(progress, new MorningContextResolver(Configs()));
            var result = Run(service.ContinueToPreparationAsync(CancellationToken.None));

            Assert.IsNull(result);
            Assert.AreEqual(1, phaseChangedCount);
            Assert.AreEqual(DayPhase.Results, emitted.CurrentPhase);
        }
    }
}
