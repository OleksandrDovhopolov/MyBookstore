using Book.Sell.Services;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor
{
    public sealed class InteractionLockTests
    {
        [Test]
        public void Acquire_WhenFree_Succeeds()
        {
            var sut = new InteractionLock();
            var a = new object();

            Assert.IsTrue(sut.TryAcquire(a));
            Assert.IsTrue(sut.IsHeld);
            Assert.AreSame(a, sut.CurrentHolder);
        }

        [Test]
        public void ReAcquire_ByHolder_ReturnsTrue()
        {
            var sut = new InteractionLock();
            var a = new object();
            sut.TryAcquire(a);

            Assert.IsTrue(sut.TryAcquire(a));
        }

        [Test]
        public void Acquire_WhenHeld_ReturnsFalse_AndEnqueues()
        {
            var sut = new InteractionLock();
            var a = new object();
            var b = new object();
            sut.TryAcquire(a);

            Assert.IsFalse(sut.TryAcquire(b));
            Assert.AreSame(a, sut.CurrentHolder);
        }

        [Test]
        public void Release_ThenFifoWaiterWins_RegardlessOfRetryOrder()
        {
            var sut = new InteractionLock();
            var a = new object();
            var b = new object();
            var c = new object();

            sut.TryAcquire(a);    // holder = a
            sut.TryAcquire(b);    // queue: [b]
            sut.TryAcquire(c);    // queue: [b, c]

            sut.Release(a);

            // c retries first, but it is not its turn — b is head.
            Assert.IsFalse(sut.TryAcquire(c), "c must not jump the FIFO queue.");
            Assert.IsTrue(sut.TryAcquire(b), "b is the FIFO head.");
            Assert.AreSame(b, sut.CurrentHolder);

            sut.Release(b);
            Assert.IsTrue(sut.TryAcquire(c), "c is next after b.");
        }

        [Test]
        public void Release_ByWaiter_RemovesFromQueue()
        {
            var sut = new InteractionLock();
            var a = new object();
            var b = new object();
            var c = new object();

            sut.TryAcquire(a);
            sut.TryAcquire(b);   // [b]
            sut.TryAcquire(c);   // [b, c]

            sut.Release(b);      // b left before its turn
            sut.Release(a);

            // Now c is head.
            Assert.IsTrue(sut.TryAcquire(c));
        }

        [Test]
        public void IsNextOrHolding_Works()
        {
            var sut = new InteractionLock();
            var a = new object();
            var b = new object();

            Assert.IsTrue(sut.IsNextOrHolding(a), "Free lock, empty queue → any token is next.");
            sut.TryAcquire(a);
            Assert.IsTrue(sut.IsNextOrHolding(a));
            Assert.IsFalse(sut.IsNextOrHolding(b));
        }
    }
}
