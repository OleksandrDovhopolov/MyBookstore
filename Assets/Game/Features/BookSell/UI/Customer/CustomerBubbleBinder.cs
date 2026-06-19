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
            _sales.CustomerRecommendationResolved += OnCustomerRecommendationResolved;
            _registry.CustomerVisualDespawned += OnCustomerVisualDespawned;
        }

        public void Dispose()
        {
            _sales.CustomerPhaseChanged -= OnCustomerPhaseChanged;
            _sales.BookReserved -= OnBookReserved;
            _sales.CustomerPassiveSaleHappened -= OnCustomerPassiveSaleHappened;
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
                case CustomerPhase.Approaching:
                    await EnsureBubbleAsync(customer, CustomerThoughtState.Thinking, "moving");
                    break;

                case CustomerPhase.Spawned:
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
                    await DetachBubbleAsync(customer.Id);
                    break;
            }
        }

        private void OnBookReserved(Book.Sell.Domain.Customer customer, string bookId)
        {
            EnsureBubbleAsync(customer, CustomerThoughtState.ThinkingNext, "Book locked").Forget();
        }

        private void OnCustomerPassiveSaleHappened(Book.Sell.Domain.Customer customer, PassiveSaleEvent evt)
        {
            EnsureBubbleAsync(customer, CustomerThoughtState.Comment, "Bought book").Forget();
        }

        private void OnCustomerRecommendationResolved(Book.Sell.Domain.Customer customer, RecommendationResult result)
        {
            if (result.Tier == RecommendationTier.Normal || result.Tier == RecommendationTier.Excellent)
                EnsureBubbleAsync(customer, CustomerThoughtState.Comment, "Bought book").Forget();
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
            var visual = _registry.GetById(customer.Id);
            if (visual == null || visual.BubbleAnchor == null) return;

            if (!_bubbles.TryGetValue(customer.Id, out var bubble) || bubble == null)
            {
                bubble = await _worldHud.AttachAsync<CustomerThoughtBubble>(visual.BubbleAnchor, BubbleAttachArgs);
                if (bubble == null) return;
                _bubbles[customer.Id] = bubble;
            }

            await bubble.SetStateAsync(state, payload);
        }

        private async UniTask DetachBubbleAsync(string customerId)
        {
            if (!_bubbles.Remove(customerId, out var bubble) || bubble == null) return;
            await _worldHud.DetachAsync(bubble);
        }

        private void OnCustomerVisualDespawned(CustomerVisual visual)
        {
            if (visual == null || visual.Customer == null) return;
            DetachBubbleAsync(visual.Customer.Id).Forget();
        }
    }
}
