using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Game.Configs;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    /// <summary>
    /// Stub spawner for the MVP. Builds a finite, deterministic-from-random list of customers.
    /// Each customer: Approach -> [Passive x k] -> (optional Active(request_i)) -> [Passive x m]
    /// -> CompletePurchase -> Leave, with k, m in 0..2. Active requests are drawn in config order from
    /// RequestConfig; once exhausted, remaining customers are purely passive.
    /// Count = max(requestCount, tuning.BaseCustomers).
    /// </summary>
    public sealed class DefaultCustomerSpawner : ICustomerSpawner
    {
        private const int MaxExtraPassivePerSide = 2;   // k, m in 0..2

        private readonly IConfigsService _configs;

        public DefaultCustomerSpawner(IConfigsService configs)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        //TODO нужно поменять эту логику . все первые клиенты получают активную покупку . + это зависит от количества RequestConfig.
        // скорее всего нужно несколько режимов. 1 Рандом 2 Заранее заготовленные планы. например для первых дней. 3 генерация на лету
        public IReadOnlyList<Customer> BuildCustomers(SalesSessionSetup setup, SalesTuning tuning, ISalesRandom random)
        {
            var requests = _configs.GetAll<RequestConfig>();
            var count = Math.Max(requests.Count, tuning.BaseCustomers);

            //TODO в любого покупателя  должен быть минимум 1 пассивный / активный запрос.
            var customers = new List<Customer>(count);
            for (var i = 0; i < count; i++)
            {
                var index = i;
                customers.Add(CustomerPlanBuilder.Build(
                    $"cust_{index + 1}", tuning, random,
                    buildMiddle: () =>
                    {
                        var middle = new List<ICustomerStep>();

                        var k = random.Range(1, MaxExtraPassivePerSide + 1);
                        for (var p = 0; p < k; p++) middle.Add(new PassivePurchaseStep());

                        /*if (index < requests.Count)
                            middle.Add(new ActiveRequestStep(requests[index]));

                        var m = random.Range(0, MaxExtraPassivePerSide + 1);
                        for (var p = 0; p < m; p++) middle.Add(new PassivePurchaseStep());*/

                        return middle;
                    }));
            }

            return customers;
        }
    }
}
