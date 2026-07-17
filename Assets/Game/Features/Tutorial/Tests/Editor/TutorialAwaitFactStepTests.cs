using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Tutorial.Content;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tutorial.Tests.Editor
{
    public sealed class TutorialAwaitFactStepTests
    {
        [Test]
        public async Task FactAlreadyTrue_CompletesImmediately()
        {
            var step = new TutorialAwaitFactStep("already_true", () => true, TimeSpan.FromSeconds(1));

            await step.ExecuteAsync(CancellationToken.None);

            Assert.Pass();
        }

        [Test]
        public async Task FactBecomesTrue_Completes()
        {
            var fact = false;
            var step = new TutorialAwaitFactStep("later_true", () => fact, TimeSpan.FromSeconds(1));

            var run = step.ExecuteAsync(CancellationToken.None);
            await UniTask.Yield(PlayerLoopTiming.Update);
            fact = true;
            await run;

            Assert.Pass();
        }

        [Test]
        public async Task Timeout_AutoAdvances()
        {
            var step = new TutorialAwaitFactStep("timeout", () => false, TimeSpan.FromMilliseconds(10));

            LogAssert.Expect(LogType.Warning, new Regex(@"\[Tutorial\] await fact step 'timeout' timed out; auto-advancing\."));
            await step.ExecuteAsync(CancellationToken.None);

            Assert.Pass();
        }
    }
}
