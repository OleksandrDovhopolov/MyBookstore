using System;
using System.Collections.Generic;
using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Services;
using Cysharp.Threading.Tasks;
using Game.WorldHud;
using UnityEngine;
using VContainer.Unity;

namespace Book.Sell.UI.Customer
{
    // Phase 0 binder: maps Customer phase transitions to bubble state transitions.
    //
    // Phase 0 keeps bubble content intentionally coarse: movement, locked book, active purchase,
    // and bought-book feedback. Richer rejection/reason bubbles can layer on the same events later.
    public sealed class CustomerBubbleBinder : IStartable, IDisposable
    {
        private static readonly WorldHudArgs BubbleAttachArgs =
            new(offset: Vector3.zero, billboard: false);

        private readonly ISalesDayController _sales;
        private readonly ICustomerVisualRegistry _registry;
        private readonly IWorldHudManager _worldHud;

        private readonly Dictionary<string, CustomerThoughtBubble> _bubbles = new();

        // In-flight attach tasks, keyed by customer. During an active request, AwaitingHelp and InMinigame
        // phase changes fire in the same tick, so two EnsureBubbleAsync calls run before the first
        // AttachAsync resolves; caching the task makes both share one bubble instead of spawning two.
        private readonly Dictionary<string, UniTask<CustomerThoughtBubble>> _attaching = new();

        // Detach can be requested while AttachAsync is still in flight (e.g. customer despawns before the
        // addressable bubble finishes loading). Keep the intent so the late attach result is discarded.
        private readonly HashSet<string> _detachRequested = new();

        // Customers showing a terminal bubble (passive "Failed" or "purchase completed"): it must survive
        // the Leaving/Done transition (which can fire in the same tick) and only detach on visual despawn.
        private readonly HashSet<string> _keepBubbleUntilDespawn = new();

        public CustomerBubbleBinder(
            ISalesDayController sales,
            ICustomerVisualRegistry registry,
            IWorldHudManager worldHud)
        {
            _sales = sales;
            _registry = registry;
            _worldHud = worldHud;
        }

        public void Start()
        {
            _sales.CustomerPhaseChanged += OnCustomerPhaseChanged;
            _sales.BookReserved += OnBookReserved;
            _sales.CustomerPassiveSaleHappened += OnCustomerPassiveSaleHappened;
            _sales.CustomerPassivePurchaseFailed += OnCustomerPassivePurchaseFailed;
            _sales.CustomerPurchaseCompleted += OnCustomerPurchaseCompleted;
            _sales.CustomerThoughtBubbleHidden += OnCustomerThoughtBubbleHidden;
            _sales.CustomerRecommendationResolved += OnCustomerRecommendationResolved;
            _registry.CustomerVisualDespawned += OnCustomerVisualDespawned;
        }

        public void Dispose()
        {
            _sales.CustomerPhaseChanged -= OnCustomerPhaseChanged;
            _sales.BookReserved -= OnBookReserved;
            _sales.CustomerPassiveSaleHappened -= OnCustomerPassiveSaleHappened;
            _sales.CustomerPassivePurchaseFailed -= OnCustomerPassivePurchaseFailed;
            _sales.CustomerPurchaseCompleted -= OnCustomerPurchaseCompleted;
            _sales.CustomerThoughtBubbleHidden -= OnCustomerThoughtBubbleHidden;
            _sales.CustomerRecommendationResolved -= OnCustomerRecommendationResolved;
            _registry.CustomerVisualDespawned -= OnCustomerVisualDespawned;
        }

        private void OnCustomerPhaseChanged(Book.Sell.Domain.Customer customer)
        {
            HandlePhaseAsync(customer).Forget();
        }

        private async UniTaskVoid HandlePhaseAsync(Book.Sell.Domain.Customer customer)
        {
            switch (customer.Phase)
            {
                // No bubble while the customer is spawning / walking up (Spawned, Approaching).
                // It first appears once they reach the shelf and start choosing (Browsing).
                case CustomerPhase.Browsing:
                case CustomerPhase.AwaitingHelp:
                    await EnsureBubbleAsync(customer, CustomerThoughtState.Thinking);
                    break;

                case CustomerPhase.InMinigame:
                    // Phase 0: book sprite is null (placeholder); BookPicked still shows the book sub-view
                    // so we can verify the state-machine plumbing.
                    await EnsureBubbleAsync(customer, CustomerThoughtState.BookPicked, "Active purchase");
                    break;

                case CustomerPhase.Leaving:
                case CustomerPhase.Done:
                    // Keep a terminal bubble (Failed / purchase completed) up through the walk-away;
                    // it is cleaned up on despawn.
                    if (!_keepBubbleUntilDespawn.Contains(customer.Id))
                        await DetachBubbleAsync(customer.Id);
                    break;
            }
        }

        private void OnBookReserved(Book.Sell.Domain.Customer customer, string bookId)
        {
            EnsureBubbleAsync(customer, CustomerThoughtState.ThinkingNext, "Book locked").Forget();
        }

        private void OnCustomerPassivePurchaseFailed(Book.Sell.Domain.Customer customer)
        {
            _keepBubbleUntilDespawn.Add(customer.Id);
            EnsureBubbleAsync(customer, CustomerThoughtState.PassiveSaleFailed, "Failed").Forget();
        }

        private void OnCustomerPurchaseCompleted(Book.Sell.Domain.Customer customer, int purchasedBookCount)
        {
            _keepBubbleUntilDespawn.Add(customer.Id);
            EnsureBubbleAsync(customer, CustomerThoughtState.PurchaseCompleted, $"Bought {purchasedBookCount} books").Forget();
        }

        private void OnCustomerThoughtBubbleHidden(Book.Sell.Domain.Customer customer)
        {
            // LeaveStep asked to clear the HUD: drop the keep-alive flag and detach so the customer
            // walks away without a bubble (feedback already had its dwell in the prior steps).
            _keepBubbleUntilDespawn.Remove(customer.Id);
            DetachBubbleAsync(customer.Id).Forget();
        }

        private void OnCustomerPassiveSaleHappened(Book.Sell.Domain.Customer customer, PassiveSaleEvent evt)
        {
            EnsureBubbleAsync(customer, CustomerThoughtState.Comment, "Bought book").Forget();
        }

        private void OnCustomerRecommendationResolved(Book.Sell.Domain.Customer customer, RecommendationResult result)
        {
            // The active-sale reaction is shown inside the minigame window, not in the world HUD. Detach the
            // active-request bubble (for all tiers) so it can't blink when the window closes and HUD
            // suppression is released. If the customer bought a book, CompletePurchaseStep reattaches the
            // final "Bought N books" bubble afterwards via OnCustomerPurchaseCompleted.
            DetachBubbleAsync(customer.Id).Forget();
        }

        private UniTask EnsureBubbleAsync(
            Book.Sell.Domain.Customer customer,
            CustomerThoughtState state,
            string stateText)
        {
            var payload = string.IsNullOrEmpty(stateText)
                ? CustomerThoughtPayload.Empty
                : new CustomerThoughtPayload(commentText: stateText);

            return EnsureBubbleAsync(customer, state, payload);
        }

        private UniTask EnsureBubbleAsync(Book.Sell.Domain.Customer customer, CustomerThoughtState state)
            => EnsureBubbleAsync(customer, state, CustomerThoughtPayload.Empty);

        private async UniTask EnsureBubbleAsync(
            Book.Sell.Domain.Customer customer,
            CustomerThoughtState state,
            CustomerThoughtPayload payload)
        {
            var bubble = await GetOrAttachBubbleAsync(customer);
            if (bubble == null) return;

            await bubble.SetStateAsync(state, payload);
        }

        // Returns the customer's bubble, attaching it on first use. Concurrent callers (the AwaitingHelp +
        // InMinigame burst on an active request) await the same in-flight attach, so only one bubble is ever
        // created — avoiding two overlapping world-space bubbles z-fighting (the active-purchase flicker).
        private async UniTask<CustomerThoughtBubble> GetOrAttachBubbleAsync(Book.Sell.Domain.Customer customer)
        {
            var customerId = customer.Id;
            if (_detachRequested.Contains(customerId)) return null;

            if (_bubbles.TryGetValue(customerId, out var existing) && existing != null)
                return existing;

            if (_attaching.TryGetValue(customerId, out var inFlight))
                return await inFlight;

            var visual = _registry.GetById(customerId);
            if (visual == null || visual.BubbleAnchor == null) return null;

            var attachTask = _worldHud.AttachAsync<CustomerThoughtBubble>(visual.BubbleAnchor, BubbleAttachArgs).Preserve();
            _attaching[customerId] = attachTask;
            try
            {
                var bubble = await attachTask;
                if (_detachRequested.Contains(customerId)) return null;

                if (bubble != null)
                    _bubbles[customerId] = bubble;
                return bubble;
            }
            finally
            {
                _attaching.Remove(customerId);
            }
        }

        private async UniTask DetachBubbleAsync(string customerId)
        {
            if (string.IsNullOrEmpty(customerId)) return;

            _detachRequested.Add(customerId);
            try
            {
                _bubbles.Remove(customerId, out var bubble);

                if (bubble == null && _attaching.TryGetValue(customerId, out var inFlight))
                    bubble = await inFlight;

                if (bubble == null) return;

                // If the attach continuation won the race and registered the bubble, remove it before
                // detaching so no later event can reuse a HUD that is already on its way out.
                _bubbles.Remove(customerId);
                await _worldHud.DetachAsync(bubble);
            }
            finally
            {
                _detachRequested.Remove(customerId);
            }
        }

        private void OnCustomerVisualDespawned(CustomerVisual visual)
        {
            if (visual == null || visual.Customer == null) return;
            _keepBubbleUntilDespawn.Remove(visual.Customer.Id);
            DetachBubbleAsync(visual.Customer.Id).Forget();
        }
    }
}
