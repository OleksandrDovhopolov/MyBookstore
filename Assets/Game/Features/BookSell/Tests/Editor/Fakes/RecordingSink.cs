using System.Collections.Generic;
using Book.Sell.API;
using Book.Sell.Domain;
using Game.Configs.Models;

namespace Book.Sell.Tests.Editor.Fakes
{
    /// <summary>Captures the facts steps report, for step-level assertions.</summary>
    public sealed class RecordingSink : ISalesDaySink
    {
        public List<(Customer customer, CustomerPhase phase)> Phases { get; } = new();
        public List<(Customer customer, string bookId)> Reserved { get; } = new();
        public List<(Customer customer, string bookId)> Released { get; } = new();
        public List<Customer> PassiveFailures { get; } = new();
        public List<string> PassiveFailureGenres { get; } = new();
        public List<(Customer customer, int count)> PurchaseCompletions { get; } = new();
        public List<(Customer customer, PassiveSaleEvent evt)> PassiveSales { get; } = new();
        public List<(Customer customer, RequestConfig request)> ActiveStarted { get; } = new();

        public void OnPhaseChanged(Customer customer, CustomerPhase phase)
            => Phases.Add((customer, phase));

        public void OnBookReserved(Customer customer, string bookId)
            => Reserved.Add((customer, bookId));

        public void OnBookReleased(Customer customer, string bookId)
            => Released.Add((customer, bookId));

        public void OnPassivePurchaseFailed(Customer customer, string genre)
        {
            PassiveFailures.Add(customer);
            PassiveFailureGenres.Add(genre);
        }

        public void OnPurchaseCompleted(Customer customer, int purchasedBookCount)
            => PurchaseCompletions.Add((customer, purchasedBookCount));

        public void OnPassiveSale(Customer customer, PassiveSaleEvent saleEvent)
            => PassiveSales.Add((customer, saleEvent));

        public void OnActiveRequestStarted(Customer customer, RequestConfig request)
            => ActiveStarted.Add((customer, request));

        public List<Customer> BubbleHidden { get; } = new();

        public void OnHideThoughtBubble(Customer customer)
            => BubbleHidden.Add(customer);
    }
}
