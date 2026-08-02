using System;
using System.Linq;
using System.Threading;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor
{
    public sealed class DeliveredDialoguesServiceTests
    {
        [Test]
        public void MarkDelivered_ThenIsDelivered_True()
        {
            var service = new SaveBackedDeliveredDialoguesService(new FakeSaveService());

            Assert.IsFalse(service.IsDelivered("dlg"));
            service.MarkDeliveredAsync("dlg", CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsTrue(service.IsDelivered("dlg"));
        }

        [Test]
        public void ImmediateDelivered_PersistsAcrossServiceInstances()
        {
            var save = new FakeSaveService();

            new SaveBackedDeliveredDialoguesService(save)
                .MarkDeliveredAsync("dlg", CancellationToken.None).GetAwaiter().GetResult();

            var reloaded = new SaveBackedDeliveredDialoguesService(save);
            Assert.IsTrue(reloaded.IsDelivered("dlg"));
            Assert.IsFalse(reloaded.IsDelivered("other"));
        }

        [Test]
        public void MarkDelivered_BlankId_IsNoOp()
        {
            var save = new FakeSaveService();
            var service = new SaveBackedDeliveredDialoguesService(save);

            service.MarkDeliveredAsync("  ", CancellationToken.None).GetAwaiter().GetResult();
            service.MarkDeliveredDeferredAsync("  ", CancellationToken.None).GetAwaiter().GetResult();
            service.CommitAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(service.IsDelivered("  "));
            Assert.AreEqual(0, save.UpdateCalls);
        }

        [Test]
        public void MarkDeliveredDeferred_IsVisibleInMemoryWithoutPersisting()
        {
            var save = new FakeSaveService();
            var service = new SaveBackedDeliveredDialoguesService(save);

            service.MarkDeliveredDeferredAsync("dlg", CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(service.IsDelivered("dlg"));
            Assert.AreEqual(0, save.UpdateCalls);
            Assert.IsFalse(new SaveBackedDeliveredDialoguesService(save).IsDelivered("dlg"));
        }

        [Test]
        public void CommitDeferred_PersistsAcrossServiceInstances()
        {
            var save = new FakeSaveService();
            var service = new SaveBackedDeliveredDialoguesService(save);
            service.MarkDeliveredDeferredAsync("dlg", CancellationToken.None).GetAwaiter().GetResult();

            service.CommitAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(new SaveBackedDeliveredDialoguesService(save).IsDelivered("dlg"));
            Assert.AreEqual(1, save.UpdateCalls);
        }

        [Test]
        public void ImmediateMark_DoesNotFlushOtherPendingIds()
        {
            var save = new FakeSaveService();
            var service = new SaveBackedDeliveredDialoguesService(save);
            service.MarkDeliveredDeferredAsync("deferred", CancellationToken.None).GetAwaiter().GetResult();

            service.MarkDeliveredAsync("immediate", CancellationToken.None).GetAwaiter().GetResult();

            var reloaded = new SaveBackedDeliveredDialoguesService(save);
            Assert.IsFalse(reloaded.IsDelivered("deferred"));
            Assert.IsTrue(reloaded.IsDelivered("immediate"));
        }

        [Test]
        public void DiscardDeferred_ClearsPendingWithoutSaveChurn()
        {
            var save = new FakeSaveService();
            var service = new SaveBackedDeliveredDialoguesService(save);
            service.MarkDeliveredDeferredAsync("dlg", CancellationToken.None).GetAwaiter().GetResult();

            service.DiscardDeferred();

            Assert.IsFalse(service.IsDelivered("dlg"));
            Assert.AreEqual(0, save.UpdateCalls);
        }

        [Test]
        public void DeferredDuplicate_CommitPersistsOnce()
        {
            var save = new FakeSaveService();
            var service = new SaveBackedDeliveredDialoguesService(save);
            service.MarkDeliveredDeferredAsync("dlg", CancellationToken.None).GetAwaiter().GetResult();
            service.MarkDeliveredDeferredAsync("dlg", CancellationToken.None).GetAwaiter().GetResult();

            service.CommitAsync(CancellationToken.None).GetAwaiter().GetResult();

            var dto = save.GetModuleAsync<DeliveredDialogues>(DialoguesSaveKeys.Delivered, CancellationToken.None)
                .GetAwaiter().GetResult();
            Assert.AreEqual(1, dto.Ids.Count(id => id == "dlg"));
            Assert.AreEqual(1, save.UpdateCalls);
        }

        [Test]
        public void MarkDelivered_NewId_RaisesChanged()
        {
            var service = new SaveBackedDeliveredDialoguesService(new FakeSaveService());
            var changed = 0;
            service.Changed += () => changed++;

            service.MarkDeliveredAsync("dlg", CancellationToken.None).GetAwaiter().GetResult();
            service.MarkDeliveredAsync("dlg", CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(1, changed);
        }

        [Test]
        public void MarkDeliveredDeferred_NewIdOnly_RaisesChanged()
        {
            var service = new SaveBackedDeliveredDialoguesService(new FakeSaveService());
            var changed = 0;
            service.Changed += () => changed++;

            service.MarkDeliveredDeferredAsync("dlg", CancellationToken.None).GetAwaiter().GetResult();
            service.MarkDeliveredDeferredAsync("dlg", CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(1, changed);
        }

        [Test]
        public void CommitDeferred_WhenCommittedSetChanges_RaisesChanged()
        {
            var service = new SaveBackedDeliveredDialoguesService(new FakeSaveService());
            var changed = 0;
            service.Changed += () => changed++;

            service.MarkDeliveredDeferredAsync("dlg", CancellationToken.None).GetAwaiter().GetResult();
            service.CommitAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(2, changed, "one deferred add + one committed-set update");
        }

        [Test]
        public void DiscardDeferred_RaisesChangedOnlyWhenPendingWasNonEmpty()
        {
            var service = new SaveBackedDeliveredDialoguesService(new FakeSaveService());
            var changed = 0;
            service.Changed += () => changed++;

            service.DiscardDeferred();
            service.MarkDeliveredDeferredAsync("dlg", CancellationToken.None).GetAwaiter().GetResult();
            service.DiscardDeferred();
            service.DiscardDeferred();

            Assert.AreEqual(2, changed, "one deferred add + one non-empty discard");
        }
    }
}
