using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.DayCycle.Day;
using Game.DayCycle.Results.UI;
using Game.Rewards.API;
using Game.Rewards.UI;
using Game.Shop.API;
using Game.Tutorial.API;
using Game.Tutorial.Content;
using Game.Tutorial.Presentation;
using Game.UI;
using Infrastructure.TutorialUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
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
        public async System.Threading.Tasks.Task GrantBox_BuysTutorialLotAndShowsRewardsWindow()
        {
            var granted = new RewardSpec(
                "book_box_genre_heartfelt_1",
                new[] { RewardItem.InventoryItem("book_001", "book", 1) });
            var shop = new FakeShopService
            {
                Result = ShopPurchaseResult.Ok(TutorialLot(), granted)
            };
            var ui = new FakeUIManager();
            var sequence = new TutorialHub(new FakeDayProgress(), ui, shop: shop);
            var step = (TutorialAsyncActionStep)sequence.GetSteps()[5];

            await step.ExecuteAsync(CancellationToken.None);

            Assert.AreEqual("tutorial_book_box_heartfelt", shop.BoughtLotId);
            Assert.AreEqual(1, shop.BuyCalls);
            Assert.AreEqual(typeof(RewardsWindow), ui.LastShownWindowType);
            Assert.IsInstanceOf<RewardsWindowArgs>(ui.LastShownArgs);

            var args = (RewardsWindowArgs)ui.LastShownArgs;
            Assert.AreSame(granted, args.Granted);
            Assert.AreEqual("Your first book box!", args.Title);
        }

        [Test]
        public async System.Threading.Tasks.Task GrantBox_NonSuccessCompletesWithoutShowingRewardsWindow()
        {
            var shop = new FakeShopService
            {
                Result = ShopPurchaseResult.Fail(ShopPurchaseStatus.LimitReached, TutorialLot())
            };
            var ui = new FakeUIManager();
            var sequence = new TutorialHub(new FakeDayProgress(), ui, shop: shop);
            var step = (TutorialAsyncActionStep)sequence.GetSteps()[5];

            LogAssert.Expect(
                LogType.Warning,
                "[Tutorial] hub gift lot 'tutorial_book_box_heartfelt' failed: LimitReached.");

            await step.ExecuteAsync(CancellationToken.None);

            Assert.AreEqual("tutorial_book_box_heartfelt", shop.BoughtLotId);
            Assert.AreEqual(1, shop.BuyCalls);
            Assert.IsNull(ui.LastShownWindowType);
            Assert.IsNull(ui.LastShownArgs);
        }

        private static ShopLot TutorialLot()
            => new(
                "tutorial_book_box_heartfelt",
                "tutorial",
                new ShopPrice("gold", 0),
                "book_box_genre_heartfelt_1",
                ShopLotLimit.Disposable(1));

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
            public Type LastShownWindowType { get; private set; }
            public WindowArgs LastShownArgs { get; private set; }

            public UniTask<T> ShowAsync<T>(WindowArgs args = null, CancellationToken ct = default)
                where T : class, IWindowController, new()
            {
                LastShownWindowType = typeof(T);
                LastShownArgs = args;
                return UniTask.FromResult<T>(null);
            }

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

        private sealed class FakeShopService : IShopService
        {
            public ShopPurchaseResult Result { get; set; } =
                ShopPurchaseResult.Fail(ShopPurchaseStatus.InternalError);

            public string BoughtLotId { get; private set; }
            public int BuyCalls { get; private set; }

            public event Action<ShopPurchaseEvent> LotPurchased;

            public IReadOnlyList<ShopLot> GetLots(string storefrontId) => Array.Empty<ShopLot>();
            public bool TryGetLot(string lotId, out ShopLot lot)
            {
                lot = null;
                return false;
            }

            public int GetPurchaseCount(string lotId) => 0;
            public bool IsAvailable(string lotId) => true;

            public UniTask<ShopPurchaseResult> BuyAsync(string lotId, CancellationToken ct)
            {
                BoughtLotId = lotId;
                BuyCalls++;
                return UniTask.FromResult(Result);
            }
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
