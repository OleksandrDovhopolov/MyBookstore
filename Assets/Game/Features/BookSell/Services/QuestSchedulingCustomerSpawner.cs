using System;
using System.Collections.Generic;
using Book.Sell.API;
using Book.Sell.Domain;
using Game.Configs;
using Game.Configs.Models;
using UnityEngine;

namespace Book.Sell.Services
{
    /// <summary>
    /// Decorator over any base <see cref="ICustomerSpawner"/> that PREPENDS one quest character per
    /// <see cref="SalesSessionSetup.ScheduledDialogueIds"/> entry (GAME-6 §Этап 5, B4) — so the scripted
    /// dialogue arrives first, then the ordinary passive crowd. Scheduling is orthogonal to the base day
    /// composition, hence a decorator rather than edits to the throwaway spawners.
    ///
    /// Each id is validated with <see cref="IConfigsService.TryGet{T}"/> (not <c>Get</c>, which would log its
    /// own not-found warning → a double warning); an unknown id warns and is skipped. Valid ids build a
    /// <see cref="QuestCharacterArchetype"/> plan through <see cref="CustomerPlanBuilder"/>, matching the
    /// production shape (Approach → DialogStep → Passive… → CompletePurchase → Leave).
    /// </summary>
    public sealed class QuestSchedulingCustomerSpawner : ICustomerSpawner
    {
        private const string LogPrefix = "[Sales.QuestSchedule]";

        private readonly ICustomerSpawner _inner;
        private readonly IConfigsService _configs;

        public QuestSchedulingCustomerSpawner(ICustomerSpawner inner, IConfigsService configs)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        public IReadOnlyList<Customer> BuildCustomers(SalesSessionSetup setup, SalesTuning tuning, ISalesRandom random)
        {
            var baseCustomers = _inner.BuildCustomers(setup, tuning, random);

            var scheduled = setup?.ScheduledDialogueIds;
            if (scheduled == null || scheduled.Count == 0)
                return baseCustomers;

            var questCustomers = new List<Customer>(scheduled.Count);
            foreach (var dialogueId in scheduled)
            {
                if (string.IsNullOrWhiteSpace(dialogueId)) continue;

                if (!_configs.TryGet<DialogueConfig>(dialogueId, out _))
                {
                    Debug.LogWarning($"{LogPrefix} scheduled dialogue '{dialogueId}' has no DialogueConfig — skipped.");
                    continue;
                }

                var archetype = new QuestCharacterArchetype(new DialoguePayload(dialogueId));
                questCustomers.Add(CustomerPlanBuilder.Build(
                    $"quest_{dialogueId}", tuning, random,
                    buildMiddle: () => archetype.BuildMiddle(setup, tuning, random)));
            }

            if (questCustomers.Count == 0)
                return baseCustomers;

            // Prepend — quest characters arrive first.
            var result = new List<Customer>(questCustomers.Count + baseCustomers.Count);
            result.AddRange(questCustomers);
            result.AddRange(baseCustomers);
            return result;
        }
    }
}
