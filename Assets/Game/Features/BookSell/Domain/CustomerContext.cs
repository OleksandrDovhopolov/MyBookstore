using Book.Sell.Services;
using Game.Configs.Models;

namespace Book.Sell.Domain
{
    /// <summary>
    /// Shared services a customer's steps operate on during a sales day. One instance per day,
    /// passed to every step tick. Holds no per-customer mutable state (the owning customer is
    /// passed explicitly into each step call).
    /// </summary>
    public sealed class CustomerContext
    {
        public SalesShelf Shelf { get; }
        public IInteractionLock Lock { get; }
        public ISalesRandom Random { get; }
        public IPassiveSaleSelector PassiveSelector { get; }
        public LocationConfig Location { get; }
        public ISalesDaySink Sink { get; }
        public SalesTuning Tuning { get; }

        public CustomerContext(
            SalesShelf shelf,
            IInteractionLock interactionLock,
            ISalesRandom random,
            IPassiveSaleSelector passiveSelector,
            LocationConfig location,
            ISalesDaySink sink,
            SalesTuning tuning)
        {
            Shelf = shelf;
            Lock = interactionLock;
            Random = random;
            PassiveSelector = passiveSelector;
            Location = location;
            Sink = sink;
            Tuning = tuning;
        }
    }
}
