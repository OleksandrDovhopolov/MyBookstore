using System.Threading;
using Game.Progression.API;
using Game.Progression.Services;
using Game.Progression.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Game.Progression.Tests.Editor
{
    public sealed class ProgressionServiceTests
    {
        private static (ProgressionService svc, FakeProgressionRepository repo, FakeSaveService save) Build()
        {
            var save = new FakeSaveService();
            var repo = new FakeProgressionRepository();
            var svc = new ProgressionService(save, repo);
            svc.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            return (svc, repo, save);
        }

        [Test]
        public void Constructor_SelfRegistersAsSaveHook()
        {
            var (svc, _, save) = Build();
            CollectionAssert.Contains(save.RegisteredHooks, svc);
        }

        [Test]
        public void AddPositive_Increments_FiresChanged()
        {
            var (svc, _, _) = Build();
            ProgressionChangeEvent captured = null;
            svc.Changed += e => captured = e;

            svc.AddReputationAsync(3, "results_day_1", CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(3, svc.Reputation);
            Assert.AreEqual(0, captured.OldReputation);
            Assert.AreEqual(3, captured.NewReputation);
            Assert.AreEqual(3, captured.Delta);
            Assert.AreEqual("results_day_1", captured.Reason);
        }

        [Test]
        public void AddNegative_WithinBounds_Decrements()
        {
            var (svc, _, _) = Build();
            svc.AddReputationAsync(5, "seed", CancellationToken.None).GetAwaiter().GetResult();

            var ok = svc.AddReputationAsync(-2, "penalty", CancellationToken.None);
            ok.GetAwaiter().GetResult();

            Assert.AreEqual(3, svc.Reputation);
        }

        [Test]
        public void AddNegative_BeyondZero_ClampsAt0_ReportsEffectiveDelta()
        {
            var (svc, _, _) = Build();
            svc.AddReputationAsync(2, "seed", CancellationToken.None).GetAwaiter().GetResult();

            ProgressionChangeEvent captured = null;
            svc.Changed += e => captured = e;

            svc.AddReputationAsync(-10, "penalty", CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(0, svc.Reputation);
            Assert.AreEqual(-2, captured.Delta, "Delta is the actual change after clamp, not the requested -10.");
        }

        [Test]
        public void AddZero_NoOp()
        {
            var (svc, repo, _) = Build();
            var savesBefore = repo.SaveCallCount;
            var events = 0;
            svc.Changed += _ => events++;

            svc.AddReputationAsync(0, "noop", CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(0, events);
            Assert.AreEqual(savesBefore, repo.SaveCallCount);
        }

        [Test]
        public void Roundtrip_PreservesReputation()
        {
            var (svc, repo, _) = Build();
            svc.AddReputationAsync(7, "x", CancellationToken.None).GetAwaiter().GetResult();

            var svc2 = new ProgressionService(new FakeSaveService(), repo);
            svc2.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(7, svc2.Reputation);
        }
    }
}
