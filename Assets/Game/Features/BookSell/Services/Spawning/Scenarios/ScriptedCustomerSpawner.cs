using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using Game.Configs;
using Game.Configs.Models;
using UnityEngine;

namespace Book.Sell.Services
{
    /// <summary>
    /// Decorator that replaces regular customer slots with questless day-scripted customers.
    /// </summary>
    public sealed class ScriptedCustomerSpawner : ICustomerSpawner
    {
        private const string LogPrefix = "[Sales.CustomerScript]";

        private readonly ICustomerSpawner _inner;
        private readonly IConfigsService _configs;

        public ScriptedCustomerSpawner(ICustomerSpawner inner, IConfigsService configs)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        public IReadOnlyList<Customer> BuildCustomers(SalesSessionSetup setup, SalesTuning tuning, ISalesRandom random)
        {
            var baseCustomers = _inner.BuildCustomers(setup, tuning, random);
            if (baseCustomers.Count == 0)
                return baseCustomers;

            var scriptedCustomers = BuildScriptedCustomers(setup, tuning, random, baseCustomers.Count);
            if (scriptedCustomers.Count == 0)
                return baseCustomers;

            var result = new List<Customer>(baseCustomers.Count);
            result.AddRange(scriptedCustomers);

            for (var i = scriptedCustomers.Count; i < baseCustomers.Count; i++)
                result.Add(baseCustomers[i]);

            return result;
        }

        private List<Customer> BuildScriptedCustomers(
            SalesSessionSetup setup,
            SalesTuning tuning,
            ISalesRandom random,
            int capacity)
        {
            var scriptedCustomers = new List<Customer>(capacity);
            foreach (var script in _configs.GetAll<CustomerScriptConfig>())
            {
                if (script == null || script.DayIndex != setup.Day) continue;

                if (scriptedCustomers.Count >= capacity)
                {
                    Debug.LogWarning($"{LogPrefix} replacement capacity exhausted for day={setup.Day}; extra customer scripts skipped.");
                    break;
                }

                if (string.IsNullOrWhiteSpace(script.Id))
                {
                    Debug.LogWarning($"{LogPrefix} day={setup.Day} has a customer script with no id; skipped.");
                    continue;
                }

                var scriptedPlan = ScriptedPassivePlanFactory.Build(script.PassiveAttempts);
                if (scriptedPlan == null || scriptedPlan.Count == 0)
                {
                    Debug.LogWarning($"{LogPrefix} script '{script.Id}' has no passive attempts; skipped.");
                    continue;
                }

                var passiveCount = ScriptedPassivePlanFactory.PassiveCountFor(scriptedPlan);
                var archetype = new PassiveAttemptsArchetype(passiveCount, passiveCount);
                scriptedCustomers.Add(CustomerPlanBuilder.Build(
                    $"script_{script.Id}", tuning, random,
                    buildMiddle: () => archetype.BuildMiddle(setup, tuning, random),
                    characterId: script.CharacterId,
                    scriptedPassivePlan: scriptedPlan));
            }

            return scriptedCustomers;
        }
    }
}
