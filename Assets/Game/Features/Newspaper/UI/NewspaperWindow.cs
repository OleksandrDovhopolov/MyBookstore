using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Decor;
using Game.UI;
using UnityEngine;
using VContainer;

namespace Game.Newspaper.UI
{
    /// <summary>
    /// Daily newspaper window. Phase 0 scope: two decor offers (free + paid) routed through
    /// <see cref="IDecorRewardService"/>. Future content (book bundles for sale, weather forecast,
    /// weekly calendar) lives in the same prefab/view and lands in later phases.
    /// </summary>
    [Window("NewspaperWindow", WindowType.Page)]
    public sealed class NewspaperWindow : WindowController<NewspaperWindowView>
    {
        private IDecorRewardService _decorReward;
        private CancellationTokenSource _cts;

        [Inject]
        public void InjectServices(IDecorRewardService decorReward)
        {
            _decorReward = decorReward;
        }

        protected override void OnInit()
        {
            _cts = new CancellationTokenSource();
            View.FreeDecorClaimButton.onClick.AddListener(OnClaimFreeDecorClicked);
            View.PaidDecorBuyButton.onClick.AddListener(OnBuyPaidDecorClicked);
            View.CloseButton.onClick.AddListener(OnCloseClicked);
        }

        protected override void OnShowStart() => RefreshDecorOffers();

        protected override void UpdateWindow() => RefreshDecorOffers();

        protected override void OnDispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (View == null) return;
            View.FreeDecorClaimButton.onClick.RemoveListener(OnClaimFreeDecorClicked);
            View.PaidDecorBuyButton.onClick.RemoveListener(OnBuyPaidDecorClicked);
            View.CloseButton.onClick.RemoveListener(OnCloseClicked);
        }

        private void RefreshDecorOffers()
        {
            if (_decorReward == null) return;

            if (View.FreeDecorPanel != null)
            {
                View.FreeDecorPanel.SetActive(_decorReward.HasFreeDecorAvailable);
                if (View.FreeDecorLabel != null)
                    View.FreeDecorLabel.text = $"Newspaper: free <b>{_decorReward.OfferedFreeDecorId}</b>!";
            }

            if (View.PaidDecorPanel != null)
            {
                View.PaidDecorPanel.SetActive(_decorReward.HasPaidOfferAvailable);
                if (View.PaidDecorLabel != null)
                    View.PaidDecorLabel.text = $"Buy <b>{_decorReward.OfferedPaidDecorId}</b> — {_decorReward.OfferedPaidPrice} gold";
            }
        }

        private void OnClaimFreeDecorClicked() => ClaimFreeDecorAsync().Forget();

        private async UniTaskVoid ClaimFreeDecorAsync()
        {
            if (_decorReward == null) return;
            await _decorReward.ClaimFreeDecorAsync(_cts.Token);
            RefreshDecorOffers();
        }

        private void OnBuyPaidDecorClicked() => BuyPaidDecorAsync().Forget();

        private async UniTaskVoid BuyPaidDecorAsync()
        {
            if (_decorReward == null) return;
            var bought = await _decorReward.BuyOfferedDecorAsync(_cts.Token);
            if (!bought)
                Debug.Log("[NewspaperWindow] Paid decor purchase failed (likely insufficient gold).");
            RefreshDecorOffers();
        }

        private void OnCloseClicked() => CloseAsync().Forget();
    }
}
