using System.Threading;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor
{
    /// <summary>
    /// GAME-6 fire-once store. Mark → IsDelivered true; unknown id false; and the record survives a fresh
    /// service instance over the same save (persist), which is what makes the intro not replay next launch.
    /// </summary>
    public sealed class DeliveredDialoguesServiceTests
    {
        [Test]
        public void MarkDelivered_ThenIsDelivered_True()
        {
            var service = new SaveBackedDeliveredDialoguesService(new FakeSaveService());

            Assert.IsFalse(service.IsDelivered("dlg"), "Unknown id is not delivered.");
            service.MarkDeliveredAsync("dlg", CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsTrue(service.IsDelivered("dlg"));
        }

        [Test]
        public void Delivered_PersistsAcrossServiceInstances()
        {
            var save = new FakeSaveService();

            new SaveBackedDeliveredDialoguesService(save)
                .MarkDeliveredAsync("dlg", CancellationToken.None).GetAwaiter().GetResult();

            // A fresh service over the SAME save (simulates a later session) still sees it.
            var reloaded = new SaveBackedDeliveredDialoguesService(save);
            Assert.IsTrue(reloaded.IsDelivered("dlg"));
            Assert.IsFalse(reloaded.IsDelivered("other"));
        }

        [Test]
        public void MarkDelivered_BlankId_IsNoOp()
        {
            var save = new FakeSaveService();
            var service = new SaveBackedDeliveredDialoguesService(save);

            Assert.DoesNotThrow(() =>
                service.MarkDeliveredAsync("  ", CancellationToken.None).GetAwaiter().GetResult());
            Assert.IsFalse(service.IsDelivered("  "));
            Assert.AreEqual(0, save.UpdateCalls);
        }

        [Test]
        public void Clear_RemovesDeliveredId_AndPersists()
        {
            var save = new FakeSaveService();
            var service = new SaveBackedDeliveredDialoguesService(save);
            service.MarkDeliveredAsync("dlg", CancellationToken.None).GetAwaiter().GetResult();

            service.ClearAsync("dlg", CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(service.IsDelivered("dlg"));
            var reloaded = new SaveBackedDeliveredDialoguesService(save);
            Assert.IsFalse(reloaded.IsDelivered("dlg"));
        }

        [Test]
        public void Clear_MissingOrBlankId_IsNoOpWithoutSaveChurn()
        {
            var save = new FakeSaveService();
            var service = new SaveBackedDeliveredDialoguesService(save);
            service.MarkDeliveredAsync("dlg", CancellationToken.None).GetAwaiter().GetResult();
            var callsAfterMark = save.UpdateCalls;

            service.ClearAsync("missing", CancellationToken.None).GetAwaiter().GetResult();
            service.ClearAsync("  ", CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(callsAfterMark, save.UpdateCalls);
            Assert.IsTrue(service.IsDelivered("dlg"));
        }
    }
}
