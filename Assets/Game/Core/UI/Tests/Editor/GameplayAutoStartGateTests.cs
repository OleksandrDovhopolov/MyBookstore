using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Core.UI.Tests.Editor
{
    public sealed class GameplayAutoStartGateTests
    {
        [Test]
        public void BlockRelease_TogglesBlockedStateAndFiresReleased()
        {
            var gate = new GameplayAutoStartGate();
            var releasedCount = 0;
            gate.Released += () => releasedCount++;

            gate.Block();

            Assert.IsTrue(gate.IsBlocked);
            Assert.AreEqual(0, releasedCount);

            gate.Release();

            Assert.IsFalse(gate.IsBlocked);
            Assert.AreEqual(1, releasedCount);
        }

        [Test]
        public void NestedBlocks_ReleaseFiresOnlyWhenLastBlockIsReleased()
        {
            var gate = new GameplayAutoStartGate();
            var releasedCount = 0;
            gate.Released += () => releasedCount++;

            gate.Block();
            gate.Block();

            gate.Release();

            Assert.IsTrue(gate.IsBlocked);
            Assert.AreEqual(0, releasedCount);

            gate.Release();

            Assert.IsFalse(gate.IsBlocked);
            Assert.AreEqual(1, releasedCount);
        }

        [Test]
        public void BlockAfterRelease_CanFireReleasedAgain()
        {
            var gate = new GameplayAutoStartGate();
            var releasedCount = 0;
            gate.Released += () => releasedCount++;

            gate.Block();
            gate.Release();
            gate.Block();
            gate.Release();

            Assert.IsFalse(gate.IsBlocked);
            Assert.AreEqual(2, releasedCount);
        }

        [Test]
        public void ReleaseWithoutBlock_LogsWarningAndDoesNotFireReleased()
        {
            var gate = new GameplayAutoStartGate();
            var releasedCount = 0;
            gate.Released += () => releasedCount++;

            LogAssert.Expect(LogType.Warning, "[GameplayAutoStartGate] Release called without a matching Block.");

            gate.Release();

            Assert.IsFalse(gate.IsBlocked);
            Assert.AreEqual(0, releasedCount);
        }
    }
}
