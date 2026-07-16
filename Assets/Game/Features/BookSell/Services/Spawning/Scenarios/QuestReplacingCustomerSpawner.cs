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
        private readonly ICustomerProfileProvider _profiles;

        public QuestReplacingCustomerSpawner(
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

                var archetype = new QuestCharacterArchetype(
                    new DialoguePayload(dialogueId),
                    PassiveCountFor(quest.CharacterId));
                questCustomers.Add(CustomerPlanBuilder.Build(
                    $"quest_{quest.Id}", tuning, random,
                    buildMiddle: () => archetype.BuildMiddle(setup, tuning, random),
                    buildProfile: () => BuildProfile(quest, setup, random),
                    characterId: quest.CharacterId));
            }

            return questCustomers;
        }

        private int PassiveCountFor(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return 1;
            if (!_configs.TryGet<CharacterConfig>(characterId, out var character)) return 1;

            var script = character.ScriptedPassivePurchases;
            return script is { Length: > 0 } ? script.Length : 1;
        }

        private CustomerProfile BuildProfile(IQuest quest, SalesSessionSetup setup, ISalesRandom random)
        {
            var fallback = _profiles.Create(setup, random);
            var characterId = quest?.CharacterId;
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
