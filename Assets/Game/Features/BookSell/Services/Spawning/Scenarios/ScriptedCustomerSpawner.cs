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
    /// Decorator that replaces regular customer slots with day- or quest-scripted customers.
    /// </summary>
    public sealed class ScriptedCustomerSpawner : ICustomerSpawner
    {
        private const string LogPrefix = "[Sales.CustomerScript]";

        private readonly ICustomerSpawner _inner;
        private readonly IConfigsService _configs;
        private readonly IQuestsService _quests;
        private readonly IDeliveredDialoguesService _delivered;
        private readonly ICustomerProfileProvider _profiles;

        public ScriptedCustomerSpawner(
            ICustomerSpawner inner,
            IConfigsService configs,
            IQuestsService quests,
            IDeliveredDialoguesService delivered,
            ICustomerProfileProvider profiles)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _quests = quests ?? throw new ArgumentNullException(nameof(quests));
            _delivered = delivered ?? throw new ArgumentNullException(nameof(delivered));
            _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
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
                if (script == null) continue;
                if (!IsEligible(script, setup)) continue;

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

                var scriptedPlan = ScriptedPassivePlanFactory.Build(script.PassiveAttempts);
                if (scriptedPlan == null || scriptedPlan.Count == 0)
                {
                    Debug.LogWarning($"{LogPrefix} script '{script.Id}' has no passive attempts; skipped.");
                    continue;
                }

                var passiveCount = ScriptedPassivePlanFactory.PassiveCountFor(scriptedPlan);
                ICustomerArchetype archetype = !string.IsNullOrWhiteSpace(dialogueId)
                    ? new QuestCharacterArchetype(new DialoguePayload(dialogueId), passiveCount)
                    : new PassiveAttemptsArchetype(passiveCount, passiveCount);
                scriptedCustomers.Add(CustomerPlanBuilder.Build(
                    $"script_{script.Id}", tuning, random,
                    buildMiddle: () => archetype.BuildMiddle(setup, tuning, random),
                    buildProfile: () => BuildProfile(script, setup, random),
                    characterId: script.CharacterId,
                    scriptedPassivePlan: scriptedPlan));
            }

            return scriptedCustomers;
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
                return script.DayIndex.Value == setup.Day;

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
    }
}
