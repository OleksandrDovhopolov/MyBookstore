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
    /// Decorator that injects day- or quest-scripted customers. Scripts with authored sales attempts
    /// replace regular customer slots; dialogue-only story visits are additive.
    /// </summary>
    public sealed class ScriptedCustomerSpawner : ICustomerSpawner
    {
        private const string LogPrefix = "[Sales.CustomerScript]";

        private readonly ICustomerSpawner _inner;
        private readonly IConfigsService _configs;
        private readonly IQuestsService _quests;
        private readonly IDeliveredDialoguesService _delivered;
        private readonly ICustomerProfileProvider _profiles;
        private readonly IActiveRequestRuntimeProvider _activeRequests;
        private readonly IActiveRequestSelectorFactory _selectorFactory;

        public ScriptedCustomerSpawner(
            ICustomerSpawner inner,
            IConfigsService configs,
            IQuestsService quests,
            IDeliveredDialoguesService delivered,
            ICustomerProfileProvider profiles,
            IActiveRequestRuntimeProvider activeRequests,
            IActiveRequestSelectorFactory selectorFactory)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _quests = quests ?? throw new ArgumentNullException(nameof(quests));
            _delivered = delivered ?? throw new ArgumentNullException(nameof(delivered));
            _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
            _activeRequests = activeRequests ?? throw new ArgumentNullException(nameof(activeRequests));
            _selectorFactory = selectorFactory ?? throw new ArgumentNullException(nameof(selectorFactory));
        }

        public IReadOnlyList<Customer> BuildCustomers(SalesSessionSetup setup, SalesTuning tuning, ISalesRandom random)
        {
            var baseCustomers = _inner.BuildCustomers(setup, tuning, random);
            if (baseCustomers.Count == 0)
                return baseCustomers;

            var scriptedVisits = BuildScriptedCustomers(setup, tuning, random, baseCustomers.Count);
            if (scriptedVisits.Count == 0)
                return baseCustomers;

            var replacedSlots = 0;
            var result = new List<Customer>(baseCustomers.Count + scriptedVisits.Count);
            for (var i = 0; i < scriptedVisits.Count; i++)
            {
                result.Add(scriptedVisits[i].Customer);
                if (scriptedVisits[i].ReplacesRegularSlot)
                    replacedSlots++;
            }

            for (var i = replacedSlots; i < baseCustomers.Count; i++)
                result.Add(baseCustomers[i]);

            return result;
        }

        private List<ScriptedCustomerVisit> BuildScriptedCustomers(
            SalesSessionSetup setup,
            SalesTuning tuning,
            ISalesRandom random,
            int capacity)
        {
            var selector = _selectorFactory.CreateForDay(_activeRequests.GetRequests());
            var scriptedCustomers = new List<ScriptedCustomerVisit>();
            var replacedSlots = 0;

            foreach (var script in _configs.GetAll<CustomerScriptConfig>())
            {
                if (script == null) continue;
                if (!IsEligible(script, setup)) continue;

                if (string.IsNullOrWhiteSpace(script.Id))
                {
                    Debug.LogWarning($"{LogPrefix} day={setup.Day} has a customer script with no id; skipped.");
                    continue;
                }

                var dialogueId = script.DialogueId;
                if (!string.IsNullOrWhiteSpace(dialogueId))
                {
                    if (_delivered.IsDelivered(dialogueId)) continue;

                    if (!_configs.TryGet<DialogueConfig>(dialogueId, out _))
                    {
                        Debug.LogWarning($"{LogPrefix} script '{script.Id}' references dialogue '{dialogueId}' with no DialogueConfig; skipped.");
                        continue;
                    }
                }

                var hasDialogue = !string.IsNullOrWhiteSpace(dialogueId);
                var wantsActiveRequest = script.ActiveRequest;
                var scriptedPlan = ScriptedPassivePlanFactory.Build(script.PassiveAttempts);
                var hasScriptedPassive = scriptedPlan != null && scriptedPlan.Count > 0;
                if (!hasDialogue && !hasScriptedPassive && !wantsActiveRequest)
                {
                    Debug.LogWarning($"{LogPrefix} script '{script.Id}' has no passive attempts or active request; skipped.");
                    continue;
                }

                var replacesRegularSlot = hasScriptedPassive || wantsActiveRequest || !hasDialogue;
                if (replacesRegularSlot && replacedSlots >= capacity)
                {
                    Debug.LogWarning($"{LogPrefix} replacement capacity exhausted for day={setup.Day}; extra customer scripts skipped.");
                    continue;
                }

                var profile = BuildProfile(script, setup, random);
                var request = wantsActiveRequest ? selector.Draw(profile, random) : null;
                var passiveCount = ScriptedPassivePlanFactory.PassiveCountFor(scriptedPlan);
                var archetype = BuildArchetype(dialogueId, hasDialogue, hasScriptedPassive, passiveCount, request);

                var customer = CustomerPlanBuilder.Build(
                    $"script_{script.Id}", tuning, random,
                    buildMiddle: () => archetype.BuildMiddle(setup, tuning, random),
                    profile: profile,
                    characterId: script.CharacterId,
                    scriptedPassivePlan: scriptedPlan);

                scriptedCustomers.Add(new ScriptedCustomerVisit(customer, replacesRegularSlot));
                if (replacesRegularSlot)
                    replacedSlots++;
            }

            return scriptedCustomers;
        }

        private static ICustomerArchetype BuildArchetype(
            string dialogueId,
            bool hasDialogue,
            bool hasScriptedPassive,
            int passiveCount,
            ActiveRequestRuntime request)
        {
            ICustomerArchetype salesArchetype;
            if (request != null)
            {
                salesArchetype = hasScriptedPassive
                    ? new PassiveActivePassiveArchetype(request, passiveCount, passiveCount)
                    : new ActiveRequestArchetype(request);
            }
            else
            {
                salesArchetype = new PassiveAttemptsArchetype(passiveCount, passiveCount);
            }

            if (!hasDialogue)
                return salesArchetype;

            var afterDialogue = hasScriptedPassive || request != null ? salesArchetype : null;
            return new QuestCharacterArchetype(new DialoguePayload(dialogueId), afterDialogue);
        }

        private bool IsEligible(CustomerScriptConfig script, SalesSessionSetup setup)
        {
            var hasDay = script.DayIndex.HasValue;
            var hasQuest = !string.IsNullOrWhiteSpace(script.ActivationQuestId);
            if (hasDay == hasQuest)
            {
                Debug.LogWarning($"{LogPrefix} script '{script.Id}' must set exactly one of dayIndex or activationQuestId; skipped.");
                return false;
            }

            if (hasDay)
                return CustomerScriptDayLookup.MatchesDay(script, setup.Day);

            var questId = script.ActivationQuestId;
            if (_quests.TryGetQuest(questId) == null)
            {
                Debug.LogWarning($"{LogPrefix} script '{script.Id}' references unknown activation quest '{questId}'; skipped.");
                return false;
            }

            return _quests.GetQuestState(questId) == QuestState.Active;
        }

        private CustomerProfile BuildProfile(CustomerScriptConfig script, SalesSessionSetup setup, ISalesRandom random)
        {
            var fallback = _profiles.Create(setup, random);
            var characterId = script?.CharacterId;
            if (string.IsNullOrEmpty(characterId)) return fallback;

            if (!_configs.TryGet<CharacterConfig>(characterId, out var character)) return fallback;
            var genres = character.FavoriteGenres;
            if (genres == null || genres.Length == 0) return fallback;

            WarnUnknownGenres(character.Id, genres);
            return new CustomerProfile(genres);
        }

        private void WarnUnknownGenres(string characterId, IReadOnlyList<string> genres)
        {
            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var book in _configs.GetAll<BookConfig>())
            {
                var genre = book?.PrimaryGenre;
                if (!string.IsNullOrEmpty(genre)) known.Add(genre);
            }

            for (var i = 0; i < genres.Count; i++)
            {
                var genre = genres[i];
                if (!string.IsNullOrEmpty(genre) && !known.Contains(genre))
                    Debug.LogWarning($"{LogPrefix} character '{characterId}' favorite genre '{genre}' is not present in BookConfig.PrimaryGenre.");
            }
        }

        private readonly struct ScriptedCustomerVisit
        {
            public ScriptedCustomerVisit(Customer customer, bool replacesRegularSlot)
            {
                Customer = customer;
                ReplacesRegularSlot = replacesRegularSlot;
            }

            public Customer Customer { get; }
            public bool ReplacesRegularSlot { get; }
        }
    }
}
