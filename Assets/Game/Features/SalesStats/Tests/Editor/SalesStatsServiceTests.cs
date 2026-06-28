using System.Threading;
using Game.Configs.Models;
using Game.SalesStats.API;
using Game.SalesStats.Services;
using Game.SalesStats.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Game.SalesStats.Tests.Editor
{
    public sealed class SalesStatsServiceTests
    {
        private const string CrimeBook = "book_crime";
        private const string KidsBook = "book_kids";

        private static (SalesStatsService svc, FakeSalesStatsRepository repo, FakeSaveService save) Build()
        {
            var save = new FakeSaveService();
            var repo = new FakeSalesStatsRepository();
            var configs = new FakeConfigsService()
                .Add(new BookConfig { Id = CrimeBook, Genre = BookGenre.Crime.ToConfigValue() })
                .Add(new BookConfig { Id = KidsBook, Genre = BookGenre.Kids.ToConfigValue() });

            var svc = new SalesStatsService(save, repo, configs);
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
        public void RecordSold_IncrementsGenreAndTotal_FiresChanged()
        {
            var (svc, _, _) = Build();
            SalesStatsChange captured = null;
            svc.Changed += e => captured = e;

            svc.RecordSold(CrimeBook);

            Assert.AreEqual(1, svc.GetSold(BookGenre.Crime));
            Assert.AreEqual(0, svc.GetSold(BookGenre.Kids));
            Assert.AreEqual(1, svc.TotalSold);
            Assert.IsNotNull(captured);
            Assert.AreEqual(BookGenre.Crime, captured.Genre);
            Assert.AreEqual(1, captured.NewCount);
            Assert.AreEqual(1, captured.TotalSold);
            Assert.AreEqual(CrimeBook, captured.BookId);
        }

        [Test]
        public void RecordSold_AccumulatesPerGenre()
        {
            var (svc, _, _) = Build();

            svc.RecordSold(CrimeBook);
            svc.RecordSold(CrimeBook);
            svc.RecordSold(KidsBook);

            Assert.AreEqual(2, svc.GetSold(BookGenre.Crime));
            Assert.AreEqual(1, svc.GetSold(BookGenre.Kids));
            Assert.AreEqual(3, svc.TotalSold);
        }

        [Test]
        public void RecordSold_UnknownBook_NotCounted()
        {
            var (svc, _, _) = Build();

            svc.RecordSold("does_not_exist");
            svc.RecordSold(null);
            svc.RecordSold("");

            Assert.AreEqual(0, svc.TotalSold);
        }

        [Test]
        public void RecordSold_DoesNotWritePerBook_OnlyMarksDirty()
        {
            var (svc, repo, save) = Build();
            var savesBefore = repo.SaveCallCount;

            svc.RecordSold(CrimeBook);
            svc.RecordSold(CrimeBook);
            svc.RecordSold(KidsBook);

            // Batched policy: no repository write happens during recording — only the save is flagged dirty.
            Assert.AreEqual(savesBefore, repo.SaveCallCount, "Recording must not persist per book.");
            Assert.AreEqual(3, save.MarkDirtyCount, "Each sale flags the save dirty for the debounced autosave.");
        }

        [Test]
        public void BeforeSaveAsync_WhenDirty_PersistsOnce()
        {
            var (svc, repo, _) = Build();
            svc.RecordSold(CrimeBook);
            svc.RecordSold(KidsBook);

            svc.BeforeSaveAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(1, repo.SaveCallCount);
            Assert.AreEqual(1, repo.Stored.SoldByGenre[BookGenre.Crime.ToConfigValue()]);
            Assert.AreEqual(1, repo.Stored.SoldByGenre[BookGenre.Kids.ToConfigValue()]);
        }

        [Test]
        public void BeforeSaveAsync_WhenClean_DoesNotWrite()
        {
            var (svc, repo, _) = Build();

            svc.BeforeSaveAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(0, repo.SaveCallCount);
        }

        [Test]
        public void BeforeSaveAsync_SecondCallWithoutChange_DoesNotWriteAgain()
        {
            var (svc, repo, _) = Build();
            svc.RecordSold(CrimeBook);

            svc.BeforeSaveAsync(CancellationToken.None).GetAwaiter().GetResult();
            svc.BeforeSaveAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(1, repo.SaveCallCount, "Clean state must not re-persist.");
        }

        [Test]
        public void Roundtrip_PreservesCounts()
        {
            var (svc, repo, _) = Build();
            svc.RecordSold(CrimeBook);
            svc.RecordSold(CrimeBook);
            svc.RecordSold(KidsBook);
            svc.BeforeSaveAsync(CancellationToken.None).GetAwaiter().GetResult();

            var configs = new FakeConfigsService();
            var svc2 = new SalesStatsService(new FakeSaveService(), repo, configs);
            svc2.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(2, svc2.GetSold(BookGenre.Crime));
            Assert.AreEqual(1, svc2.GetSold(BookGenre.Kids));
            Assert.AreEqual(3, svc2.TotalSold);
        }
    }
}
