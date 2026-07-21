using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.DayCycle.Day;
using Game.DayCycle.Results.UI;
using Game.Tutorial.API;
using Game.Tutorial.Content;
using Game.Tutorial.Presentation;
using Game.UI;
using Infrastructure.TutorialUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tutorial.Tests.Editor
{
    public sealed class TutorialHubTests
    {
        [Test]
        public void IsEligible_WhenDayOneCompletedAndMorning()
        {
            var dayProgress = new FakeDayProgress();
            dayProgress.Current.CompletedDays.Add(1);
            dayProgress.Current.CurrentPhase = DayPhase.Morning;
            var sequence = new TutorialHub(dayProgress, new FakeUIManager());

            Assert.IsTrue(sequence.IsEligible());
        }

        [Test]
        public void IsEligible_FalseWhenDayOneNotCompleted()
        {
            var dayProgress = new FakeDayProgress();
            dayProgress.Current.CurrentPhase = DayPhase.Morning;
            var sequence = new TutorialHub(dayProgress, new FakeUIManager());

            Assert.IsFalse(sequence.IsEligible());
        }

        [Test]
        public void IsEligible_FalseWhileResultsPhase()
        {
            var dayProgress = new FakeDayProgress();
            dayProgress.Current.CompletedDays.Add(1);
            dayProgress.Current.CurrentPhase = DayPhase.Results;
            var sequence = new TutorialHub(dayProgress, new FakeUIManager());

            Assert.IsFalse(sequence.IsEligible());
        }

        [Test]
        public void IsEligible_FalseWhileResultsWindowShown()
        {
            var dayProgress = new FakeDayProgress();
            dayProgress.Current.CompletedDays.Add(1);
            dayProgress.Current.CurrentPhase = DayPhase.Morning;
            var sequence = new TutorialHub(dayProgress, new FakeUIManager { ResultsShown = true });

            Assert.IsFalse(sequence.IsEligible());
        }

        [Test]
        public void GetSteps_ContainsHubDialogueJournalClickJournalWaitAndTerminalCloseClick()
        {
            var sequence = new TutorialHub(new FakeDayProgress(), new FakeUIManager());

            var steps = sequence.GetSteps();

            Assert.AreEqual(TutorialSequenceIds.Hub, sequence.Id);
            Assert.AreEqual(TutorialContext.Hub, sequence.Context);
            Assert.AreEqual(TutorialTrigger.HubReady, sequence.Trigger);
            Assert.AreEqual(4, steps.Count);
            Assert.IsInstanceOf<TutorialDialogueStep>(steps[0]);
            Assert.AreEqual("hub_dialogue", steps[0].Id);
            Assert.IsInstanceOf<TutorialHighlightClickStep>(steps[1]);
            Assert.AreEqual("click_journal_button", steps[1].Id);
            Assert.IsInstanceOf<TutorialAwaitWindowStep>(steps[2]);
            Assert.AreEqual("wait_journal_window", steps[2].Id);
            Assert.IsInstanceOf<TutorialHighlightClickStep>(steps[3]);
            Assert.AreEqual("click_journal_close_button", steps[3].Id);

            var closeClick = (TutorialHighlightClickStep)steps[3];
            Assert.AreEqual(TutorialTargetIds.JournalCloseButton, closeClick.TargetId);
            Assert.AreEqual(TutorialPointerPlacement.Top, closeClick.PointerPlacement);
        }

        [Test]
        public async System.Threading.Tasks.Task TerminalJournalCloseClick_CompletesStep()
        {
            var h = new OverlayHarness("TutorialHubTests_TerminalCloseClick");
            try
            {
                var registry = new TutorialTargetRegistry();
                registry.Register(TutorialTargetIds.JournalCloseButton, h.Target);
                var sequence = new TutorialHub(
                    new FakeDayProgress(),
                    new FakeUIManager(),
                    h.Overlay,
                    registry);
                var step = (TutorialHighlightClickStep)sequence.GetSteps()[3];

                var run = step.ExecuteAsync(CancellationToken.None);
                await UniTask.Yield(PlayerLoopTiming.Update);

                h.Button.onClick.Invoke();

                await run;
            }
            finally
            {
                h.Dispose();
            }
        }

        private sealed class FakeDayProgress : IDayProgressService
        {
            public event Action<DayProgressState> PhaseChanged;
            public DayProgressState Current { get; } = new();
            public UniTask<DayProgressState> LoadAsync(CancellationToken ct) => UniTask.FromResult(Current);
            public UniTask SetPhaseAsync(DayPhase phase, CancellationToken ct)
            {
                Current.CurrentPhase = phase;
                PhaseChanged?.Invoke(Current);
                return UniTask.CompletedTask;
            }

            public UniTask MarkCurrentDayCompletedAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask AdvanceToNextDayAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SaveAsync(CancellationToken ct) => UniTask.CompletedTask;
        }

        private sealed class FakeUIManager : IUIManager
        {
            private readonly LockMonitor _locks = new();

            public event Action<IWindowController> WindowShown;
            public event Action<IWindowController> WindowHidden;
            public bool ResultsShown { get; set; }

            public UniTask<T> ShowAsync<T>(WindowArgs args = null, CancellationToken ct = default)
                where T : class, IWindowController, new()
                => UniTask.FromResult<T>(null);

            public UniTask HideAsync<T>(bool forceClose = false, CancellationToken ct = default)
                where T : class, IWindowController
                => UniTask.CompletedTask;

            public UniTask HideAsync(IWindowController controller, bool forceClose = false, CancellationToken ct = default)
                => UniTask.CompletedTask;

            public UniTask HideTopAsync(WindowLayer? layer = null, CancellationToken ct = default)
                => UniTask.CompletedTask;

            public IWindowController GetTopWindow(WindowLayer? layer = null) => null;

            public bool IsWindowShown<T>() where T : class, IWindowController
                => typeof(T) == typeof(ResultsWindow) && ResultsShown;

            public bool IsWindowSpawned<T>() where T : class, IWindowController => false;
            public Game.UI.Lock SetManualLock(object owner) => _locks.Acquire(owner);
        }

        private sealed class OverlayHarness : IDisposable
        {
            private readonly TutorialOverlaySettings _settings;

            public OverlayHarness(string rootName)
            {
                Root = new GameObject(rootName, typeof(RectTransform));
                var rootRt = (RectTransform)Root.transform;
                rootRt.sizeDelta = new Vector2(800f, 600f);

                var targetGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Button));
                Target = (RectTransform)targetGo.transform;
                Target.SetParent(Root.transform, false);
                Target.anchoredPosition = new Vector2(100f, 100f);
                Target.sizeDelta = new Vector2(120f, 64f);
                Button = targetGo.GetComponent<Button>();

                _settings = TutorialOverlaySettings.CreateDefault();
                Overlay = new TutorialOverlayController(new FakeCanvasRoot(Root.transform), _settings);
            }

            public GameObject Root { get; }
            public RectTransform Target { get; }
            public Button Button { get; }
            public TutorialOverlayController Overlay { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root);
                UnityEngine.Object.DestroyImmediate(_settings);
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
