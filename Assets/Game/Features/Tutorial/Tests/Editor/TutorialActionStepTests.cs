using System;
using System.Threading;
using Game.Tutorial.Content;
using NUnit.Framework;

namespace Game.Tutorial.Tests.Editor
{
    public sealed class TutorialActionStepTests
    {
        [Test]
        public void ExecuteAsync_InvokesAction()
        {
            var calls = 0;
            var step = new TutorialActionStep("action", () => calls++);

            step.ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(1, calls);
        }

        [Test]
        public void ExecuteAsync_PreCancelledToken_DoesNotInvokeAction()
        {
            var calls = 0;
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var step = new TutorialActionStep("action", () => calls++);

            Assert.Throws<OperationCanceledException>(
                () => step.ExecuteAsync(cts.Token).GetAwaiter().GetResult());
            Assert.AreEqual(0, calls);
        }
    }
}
