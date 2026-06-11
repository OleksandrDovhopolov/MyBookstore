using System.Collections.Generic;
using Book.Sell.Domain;
using Game.Configs.Models;

namespace Book.Sell.Tests.Editor.Fakes
{
    /// <summary>Captures the facts steps report, for step-level assertions.</summary>
    public sealed class RecordingSink : ISalesDaySink
    {
        public List<(Customer customer, CustomerPhase phase)> Phases { get; } = new();
        public List<(Customer customer, PassiveSaleEvent evt)> PassiveSales { get; } = new();
        public List<(Customer customer, RequestConfig request)> ActiveStarted { get; } = new();

        public void OnPhaseChanged(Customer customer, CustomerPhase phase)
            => Phases.Add((customer, phase));

        public void OnPassiveSale(Customer customer, PassiveSaleEvent saleEvent)
            => PassiveSales.Add((customer, saleEvent));

        public void OnActiveRequestStarted(Customer customer, RequestConfig request)
            => ActiveStarted.Add((customer, request));
    }
}
