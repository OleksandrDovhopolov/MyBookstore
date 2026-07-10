using System;
using System.Collections.Generic;
using Book.Sell.API;
using Book.Sell.Domain;
using Game.Configs;
using Game.Configs.Models;
using Game.Quest.API;
using UnityEngine;

namespace Book.Sell.Services
{
    /// <summary>
    /// Decorator over any base <see cref="ICustomerSpawner"/> that PREPENDS one quest character per
    /// <b>active quest that carries a dialogue</b> (GAME-6). The schedule lives in quest state, not in the day:
    /// an active quest with a non-empty <see cref="Game.Configs.Models.QuestConfig.DialogueId"/> means its
    /// character arrives first and opens the dialogue. Fire-once: a dialogue already recorded in
    /// <see cref="IDeliveredDialoguesService"/> is skipped, so the character does not return day after day
    /// while the quest stays Active.
    ///
    /// Each dialogue id is validated with <see cref="IConfigsService.TryGet{T}"/> (not <c>Get</c>, which logs
    /// its own not-found warning); an unknown <see cref="DialogueConfig"/> warns and is skipped. Valid ids
    /// build a <see cref="QuestCharacterArchetype"/> plan through <see cref="CustomerPlanBuilder"/>, matching
    /// the production shape (Approach → DialogStep → Passive… → CompletePurchase → Leave). The customer id is
    /// derived from the quest id (unique even if two quests share a dialogue id); delivered is keyed by the
    /// dialogue id (content-level fire-once). Marking delivered happens on dialogue close (presenter), not here.
    /// </summary>
    public sealed class QuestSchedulingCustomerSpawner : ICustomerSpawner
    {
        private const string LogPrefix = "[Sales.QuestSchedule]";

        private readonly ICustomerSpawner _inner;
        private readonly IConfigsService _configs;
        private readonly IQuestsService _quests;
        private readonly IDeliveredDialoguesService _delivered;

        public QuestSchedulingCustomerSpawner(
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

            var questCustomers = new List<Customer>();
            foreach (var quest in _quests.GetActiveQuests())
            {
                var dialogueId = quest?.Config?.DialogueId;
                if (string.IsNullOrWhiteSpace(dialogueId)) continue;
                if (_delivered.IsDelivered(dialogueId)) continue;   // fire-once: already shown

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
