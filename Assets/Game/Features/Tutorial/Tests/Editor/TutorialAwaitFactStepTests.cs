using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Tutorial.Content;
using Game.Tutorial.Presentation;
using Game.UI;
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
        public async Task NoTimeout_WaitsUntilFactBecomesTrue()
        {
            var fact = false;
            var step = new TutorialAwaitFactStep("no_timeout", () => fact);

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

    public sealed class TutorialShowCalloutStepTests
    {
        [Test]
        public async Task EmptyLazyText_DoesNotCreateOverlay()
        {
            var root = new GameObject("TutorialShowCalloutStepTests_Root");
            try
            {
                var overlay = new TutorialOverlayController(
                    new FakeCanvasRoot(root.transform),
                    TutorialOverlaySettings.CreateDefault());
                var step = new TutorialShowCalloutStep("empty", overlay, () => null, "bottom");

                await step.ExecuteAsync(CancellationToken.None);

                Assert.IsNull(root.transform.Find("TutorialOverlayRoot"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public async Task LazyText_IsReadOnExecute()
        {
            var root = new GameObject("TutorialShowCalloutStepTests_Root");
            try
            {
                var text = (string)null;
                var overlay = new TutorialOverlayController(
                    new FakeCanvasRoot(root.transform),
                    TutorialOverlaySettings.CreateDefault());
                var step = new TutorialShowCalloutStep("lazy", overlay, () => text, "bottom");

                text = "Now known";
                await step.ExecuteAsync(CancellationToken.None);

                Assert.IsNotNull(root.transform.Find("TutorialOverlayRoot"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private sealed class FakeCanvasRoot : IUICanvasRoot
        {
            public FakeCanvasRoot(Transform root)
            {
                HudRoot = root;
                WindowsRoot = root;
            }

            public Transform HudRoot { get; }
            public Transform WindowsRoot { get; }
            public GameObject Blocker => null;
            public MonoBehaviour TransitionAnimation => null;
        }
    }
}
