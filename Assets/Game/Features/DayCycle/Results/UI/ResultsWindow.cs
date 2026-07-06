using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Game.DayCycle.Results.Domain;
using Game.DayCycle.Results.Services;
using Game.Inventory.API;
using Game.Newspaper.UI;
using Game.Resources.API;
using Game.Rewards.API;
using Game.UI;
using Infrastructure.ResourceAnimations;
using MessagePipe;
using UIShared;
using UnityEngine;
using VContainer;

namespace Game.DayCycle.Results.UI
{
    [Window("ResultsWindow", WindowType.Page)]
    public sealed class ResultsWindow : WindowController<ResultsWindowView>
    {
        private IResultsSessionService _service;
        private IUiSpriteProvider _uiSprites;
        private IResourceAnimationService _resourceAnimations;
        private IPublisher<ResourceCounterCountUpRequested> _countUpPublisher;
        private CancellationTokenSource _cts;
        private bool _subscribed;
        private ResultsSummary _summary;

        [Inject]
        public void InjectServices(
            IResultsSessionService service,
            IUiSpriteProvider uiSprites = null,
            IResourceAnimationService resourceAnimations = null,
            IPublisher<ResourceCounterCountUpRequested> countUpPublisher = null)
        {
            _service = service;
            _uiSprites = uiSprites;
            _resourceAnimations = resourceAnimations;
            _countUpPublisher = countUpPublisher;
        }

        protected override void OnInit()
        {
            _cts = new CancellationTokenSource();

            if (View.NextDayButton != null)
            {
                View.NextDayButton.onClick.AddListener(OnNextDayClicked);
                View.NextDayButton.interactable = false;
            }
        }

        protected override void OnShowStart()
        {
            if (_service == null)
            {
                Debug.LogWarning("[ResultsWindow] IResultsSessionService not injected.");
                return;
            }

            Subscribe();
            View.ResetView();
            if (View.NextDayButton != null) View.NextDayButton.interactable = false;
            _service.LoadAndApplyAsync(_cts.Token).Forget();
        }

        protected override void OnHideStart(bool isClosed)
        {
            base.OnHideStart(isClosed);
            Unsubscribe();
        }

        protected override void OnDispose()
        {
            Unsubscribe();

            if (View != null)
            {
                if (View.NextDayButton != null)
                    View.NextDayButton.onClick.RemoveListener(OnNextDayClicked);
            }
            
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private void Subscribe()
        {
            if (_subscribed || _service == null) return;
            _service.SummaryReady += OnSummaryReady;
            _service.NoResultAvailable += OnNoResultAvailable;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _service == null) return;
            _service.SummaryReady -= OnSummaryReady;
            _service.NoResultAvailable -= OnNoResultAvailable;
            _subscribed = false;
        }

        private void OnSummaryReady(ResultsSummary summary)
        {
            _summary = summary;
            View.SetEarnedGold(summary?.GoldEarned ?? 0);
            View.SetSoldGenres(BuildSoldGenreRewards(summary));
            LoadSoldGenreIconsAsync(_cts.Token).Forget();

            if (View.NextDayButton != null) View.NextDayButton.interactable = true;
        }

        private void OnNoResultAvailable()
        {
            if (View.NextDayButton != null)
            {
                View.NextDayButton.interactable = false;
            }
            Debug.LogError("[ResultsWindow] no SalesDayResult - Results cannot proceed.");
        }

        private void OnNextDayClicked()
        {
            Debug.Log("[ResultsWindow] NextDay clicked.");
            AdvanceToNextDayAsync().Forget();
        }

        private async UniTaskVoid AdvanceToNextDayAsync()
        {
            if (_service == null || _summary == null) return;
            if (View.NextDayButton != null) View.NextDayButton.interactable = false;

            var goldEarned = Mathf.Max(0, _summary.GoldEarned);
            var shouldAnimateGold = goldEarned > 0;
            var sourceScreenPoint = default(Vector2);
            var hasSource = shouldAnimateGold && View.TryGetCoinFlightSourceScreenPoint(out sourceScreenPoint);
            var shouldPlayFlight = false;

            try
            {
                await _service.AdvanceToNextDayAsync(_cts.Token);
                shouldPlayFlight = hasSource;

                try
                {
                    await CloseAsync(_cts.Token);
                }
                catch (OperationCanceledException)
                {
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (shouldPlayFlight)
                    PlayCoinFlightAsync(sourceScreenPoint, goldEarned).Forget();
            }
        }

        private List<RewardSpecResource> BuildSoldGenreRewards(ResultsSummary summary)
        {
            var rewards = new List<RewardSpecResource>();
            if (summary?.SoldByGenre == null) return rewards;

            foreach (BookGenre genre in Enum.GetValues(typeof(BookGenre)))
            {
                var genreId = genre.ToConfigValue();
                if (!summary.SoldByGenre.TryGetValue(genreId, out var amount) || amount <= 0) continue;

                rewards.Add(new RewardSpecResource
                {
                    ResourceId = genreId,
                    DisplayName = genreId,
                    Kind = RewardKind.InventoryItem,
                    Category = InventoryCategories.Book,
                    Amount = amount,
                    Icon = null
                });
            }

            return rewards;
        }

        private async UniTaskVoid LoadSoldGenreIconsAsync(CancellationToken ct)
        {
            if (View == null || _uiSprites == null) return;

            var entries = View.GetSoldGenreViews().ToList();

            try
            {
                foreach (var pair in entries)
                {
                    var resource = pair.Key;
                    var itemView = pair.Value;
                    if (resource == null || itemView == null) continue;

                    var sprite = await _uiSprites.GetSpriteAsync(resource.ResourceId, ct);
                    if (ct.IsCancellationRequested) return;
                    if (itemView != null) itemView.SetIcon(sprite);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async UniTaskVoid PlayCoinFlightAsync(Vector2 sourceScreenPoint, int goldEarned)
        {
            if (_resourceAnimations == null || goldEarned <= 0) return;

            try
            {
                await _resourceAnimations.PlayAsync(
                    new ResourceAnimationRequest(
                        ResourceIds.Gold,
                        goldEarned,
                        ResourceAnimationEndpoint.ScreenPoint(sourceScreenPoint),
                        ResourceAnimationEndpoint.RegisteredTarget(
                            ResourceAnimationTargetIds.Resource(ResourceIds.Gold)),
                        onParticleArrived: OnGoldParticleArrived),
                    CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
            }
        }

        // Fired as each coin lands on the HUD counter. Publishes the count-up request; the
        // presenter dedupes repeated requests for the same pack, so it is safe to fire per coin.
        private void OnGoldParticleArrived()
        {
            _countUpPublisher?.Publish(new ResourceCounterCountUpRequested(ResourceIds.Gold));
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null) target.SetActive(active);
        }
    }
}
