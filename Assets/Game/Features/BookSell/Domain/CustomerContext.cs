using System;
using System.Collections.Generic;
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
        //TODO check почему покупатель хранит какую-то информацию про локацию . скорее всего он не должен это знать  
        public LocationConfig Location { get; }
        //TODO check почему покупатель хранит какую-то информацию про декорации. скорее всего он не должен это знать  
        public IReadOnlyList<string> ActiveDecorIds { get; }
        public ISalesDaySink Sink { get; }
        public SalesTuning Tuning { get; }

        public CustomerContext(
            SalesShelf shelf,
            IInteractionLock interactionLock,
            ISalesRandom random,
            IPassiveSaleSelector passiveSelector,
            LocationConfig location,
            IReadOnlyList<string> activeDecorIds,
            ISalesDaySink sink,
            SalesTuning tuning)
        {
            Shelf = shelf;
            Lock = interactionLock;
            Random = random;
            PassiveSelector = passiveSelector;
            Location = location;
            ActiveDecorIds = activeDecorIds ?? Array.Empty<string>();
            Sink = sink;
            Tuning = tuning;
        }
    }
}
