using System;
using System.Collections.Generic;
using Book.Sell.API;
using Book.Sell.Domain;
using Dialogue;
using Game.Configs;
using Game.Configs.Models;
using Game.Quest.API;
using UnityEngine;

namespace Book.Sell.Services
{
    /// <summary>
    /// Decorator over a base <see cref="ICustomerSpawner"/> that replaces regular customer slots with
    /// quest dialogue customers instead of increasing the total visitor count.
    /// </summary>
    public sealed class QuestReplacingCustomerSpawner : ICustomerSpawner
    {
        private const string LogPrefix = "[Sales.QuestSchedule]";

        private readonly ICustomerSpawner _inner;
        private readonly IConfigsService _configs;
        private readonly IQuestsService _quests;
        private readonly IDeliveredDialoguesService _delivered;

        public QuestReplacingCustomerSpawner(
            ICustomerSpawner inner,
            IConfigsService configs,
            IQuestsService quests,
            IDeliveredDialoguesService delivered)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _quests = quests ?? throw new ArgumentNullException(nameof(quests));
            _delivered = delivered ?? throw new ArgumentNullException(nameof(delivered));
        }

        public IReadOnlyList<Customer> BuildCustomers(SalesSessionSetup setup, SalesTuning tuning, ISalesRandom random)
        {
            var baseCustomers = _inner.BuildCustomers(setup, tuning, random);
            if (baseCustomers.Count == 0)
                return baseCustomers;

            var questCustomers = BuildQuestCustomers(setup, tuning, random, baseCustomers.Count);
            if (questCustomers.Count == 0)
                return baseCustomers;

            var result = new List<Customer>(baseCustomers.Count);
            result.AddRange(questCustomers);

            for (var i = questCustomers.Count; i < baseCustomers.Count; i++)
                result.Add(baseCustomers[i]);

            return result;
        }

        private List<Customer> BuildQuestCustomers(
            SalesSessionSetup setup,
            SalesTuning tuning,
            ISalesRandom random,
            int capacity)
        {
            var questCustomers = new List<Customer>(capacity);
            foreach (var quest in _quests.GetActiveQuests())
            {
                if (questCustomers.Count >= capacity)
                {
                    Debug.LogWarning($"{LogPrefix} replacement capacity exhausted for day={setup.Day}; extra quest dialogues skipped.");
                    break;
                }

                var dialogueId = quest?.Config?.DialogueId;
                if (string.IsNullOrWhiteSpace(dialogueId)) continue;
                if (_delivered.IsDelivered(dialogueId)) continue;

                if (!_configs.TryGet<DialogueConfig>(dialogueId, out _))
                {
                    Debug.LogWarning($"{LogPrefix} quest '{quest.Id}' references dialogue '{dialogueId}' with no DialogueConfig — skipped.");
                    continue;
                }

                var archetype = new QuestCharacterArchetype(new DialoguePayload(dialogueId));
                questCustomers.Add(CustomerPlanBuilder.Build(
                    $"quest_{quest.Id}", tuning, random,
                    buildMiddle: () => archetype.BuildMiddle(setup, tuning, random)));
            }

            return questCustomers;
        }
    }
}
