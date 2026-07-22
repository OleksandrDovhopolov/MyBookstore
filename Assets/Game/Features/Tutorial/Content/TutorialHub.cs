using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Characters.UI;
using Game.DayCycle.Day;
using Game.DayCycle.Results.UI;
using Game.Rewards.UI;
using Game.Shop.API;
using Game.Tutorial.API;
using Game.Tutorial.Presentation;
using Game.UI;
using Infrastructure.TutorialUI;
using UnityEngine;

namespace Game.Tutorial.Content
{
    public sealed class TutorialHub : ITutorialSequence
    {
        private const int JournalWindowTimeoutMs = 5000;
        private const string TutorialBookBoxLotId = "tutorial_book_box_heartfelt";
        private const string BookBoxRewardTitle = "Your first book box!";

        private static readonly Vector2 JournalPointerOffset = new(0f, 100f);

        private readonly IDayProgressService _dayProgress;
        private readonly IUIManager _ui;
        private readonly TutorialOverlayController _overlay;
        private readonly ITutorialTargetRegistry _targets;
        private readonly IShopService _shop;

        public TutorialHub(
            IDayProgressService dayProgress,
            IUIManager ui = null,
            TutorialOverlayController overlay = null,
            ITutorialTargetRegistry targets = null,
            IShopService shop = null)
        {
            _dayProgress = dayProgress;
            _ui = ui;
            _overlay = overlay;
            _targets = targets;
            _shop = shop;
        }

        public string Id => TutorialSequenceIds.Hub;
        public int Priority => 30;
        public TutorialContext Context => TutorialContext.Hub;
        public TutorialTrigger Trigger => TutorialTrigger.HubReady;
        public string TriggerParam => null;
        public TutorialResumePolicy ResumePolicy => TutorialResumePolicy.Restart;

        public bool IsEligible()
        {
            var state = _dayProgress?.Current;
            return state?.CompletedDays != null
                   && state.CompletedDays.Contains(1)
                   && state.CurrentPhase == DayPhase.Morning
                   && !ResultsShown;
        }

        public IReadOnlyList<ITutorialStep> GetSteps()
            => new ITutorialStep[]
            {
                new TutorialDialogueStep("hub_dialogue", _ui, TutorialContent.Dialogues.HubIntro),
                new TutorialHighlightClickStep(
                    "click_journal_button",
                    _overlay,
                    _targets,
                    TutorialTargetIds.HubJournalButton,
                    TutorialTexts.JournalHighlight,
                    TutorialContent.Placements.Bottom,
                    pointer: true,
                    pointerPlacement: TutorialPointerPlacement.Top,
                    pointerOffset: JournalPointerOffset),
                new TutorialAwaitWindowStep(
                    "wait_journal_window",
                    () => _ui != null && _ui.IsWindowShown<JournalWindow>(),
                    JournalWindowTimeoutMs,
                    failOpen: true),
                new TutorialHighlightClickStep(
                    "click_journal_close_button",
                    _overlay,
                    _targets,
                    TutorialTargetIds.JournalCloseButton,
                    TutorialTexts.JournalCloseHighlight,
                    TutorialContent.Placements.Bottom,
                    pointer: true,
                    pointerPlacement: TutorialPointerPlacement.Top),
                new TutorialHighlightClickStep(
                    "click_get_box",
                    _overlay,
                    _targets,
                    TutorialTargetIds.HubGiftButton,
                    TutorialTexts.HubGiftHighlight,
                    TutorialContent.Placements.Bottom,
                    pointer: true,
                    pointerPlacement: TutorialPointerPlacement.Top),
                new TutorialAsyncActionStep("grant_box", GrantBoxAsync)
            };

        public void OnRunStarted() { }

        public void OnRunEnded() => _overlay?.Hide();

        private bool ResultsShown => _ui != null && _ui.IsWindowShown<ResultsWindow>();

        private async UniTask GrantBoxAsync(CancellationToken ct)
        {
            if (_shop == null)
            {
                Debug.LogWarning($"{TutorialLog.Prefix} hub gift skipped: shop service is not available.");
                return;
            }

            var result = await _shop.BuyAsync(TutorialBookBoxLotId, ct);
            if (result.Status == ShopPurchaseStatus.Success)
            {
                if (_ui != null && result.Granted != null && result.Granted.Items.Count > 0)
                {
                    _ui.ShowAsync<RewardsWindow>(
                        new RewardsWindowArgs(result.Granted, BookBoxRewardTitle),
                        ct).Forget();
                }

                return;
            }

            Debug.LogWarning($"{TutorialLog.Prefix} hub gift lot '{TutorialBookBoxLotId}' failed: {result.Status}.");
        }
    }
}
