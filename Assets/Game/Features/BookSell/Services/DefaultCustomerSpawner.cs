using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using Game.Configs;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    /// <summary>
    /// Stub spawner for the MVP. Builds a finite, deterministic-from-random list of customers.
    /// Each customer: Approach -> [Passive x k] -> CompletePurchase -> Leave, with k in 1..2.
    /// Count = max(requestCount, tuning.BaseCustomers).
    /// </summary>
    public sealed class DefaultCustomerSpawner : ICustomerSpawner
    {
        private const int MaxExtraPassivePerSide = 2;   // k in 1..2

        private readonly IConfigsService _configs;

        public DefaultCustomerSpawner(IConfigsService configs)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        //TODO нужно поменять эту логику . все первые клиенты получают активную покупку . + это зависит от количества RequestConfig.
        // скорее всего нужно несколько режимов. 1 Рандом 2 Заранее заготовленные планы. например для первых дней. 3 генерация на лету
        // Active-микс пока выключен: когда вернётся, делать day-composition как в Ten*-спавнерах
        // (PickActiveIndices + PassiveActivePassiveArchetype / ActiveRequestArchetype), а не внутри middle.
        public IReadOnlyList<Customer> BuildCustomers(SalesSessionSetup setup, SalesTuning tuning, ISalesRandom random)
        {
            var requests = _configs.GetAll<RequestConfig>();
            var count = Math.Max(requests.Count, tuning.BaseCustomers);

            var archetype = new PassiveAttemptsArchetype(1, MaxExtraPassivePerSide);
            var customers = new List<Customer>(count);
            for (var i = 0; i < count; i++)
            {
                customers.Add(CustomerPlanBuilder.Build(
                    $"cust_{i + 1}", tuning, random,
                    buildMiddle: () => archetype.BuildMiddle(setup, tuning, random)));
            }

            return customers;
        }
    }
}
