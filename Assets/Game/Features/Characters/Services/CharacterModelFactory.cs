using System;
using System.Collections.Generic;
using Game.Characters.API;
using Game.Characters.Services.Persistence;
using Game.Configs.Models;
using Game.Quest.API;

namespace Game.Characters.Services
{
    /// <summary>
    /// Assembles character read models from config + saved state + quest state
    /// (docs/CHARACTER_SYSTEM.md §6/§11):
    /// <list type="bullet">
    /// <item>memory unlocked = quest-derived (questId Awarded / chain FinalQuest Awarded) OR saved ledger.</item>
    /// <item>discovered = persisted <see cref="SavedCharacter.Discovered"/> flag (set by the service).</item>
    /// <item>journal link (id + state) describes the award quest — for a chain, its FinalQuest.</item>
    /// </list>
    /// Quest-derive rules (<see cref="IsUnlockedByQuest"/>, <see cref="IsDiscoveredByQuest"/>) are also reused
    /// by the service for reconcile/events. Unknown quest/chain resolves to Pending → locked, never throws.
    /// </summary>
    internal sealed class CharacterModelFactory : ICharacterModelFactory
    {
        private readonly IQuestsService _quests;

        public CharacterModelFactory(IQuestsService quests)
            => _quests = quests ?? throw new ArgumentNullException(nameof(quests));

        public CharacterModel Create(CharacterConfig config, SavedCharacter saved)
        {
            var memoryConfigs = config.Memories ?? Array.Empty<CharacterMemoryConfig>();
            var memories = new ICharacterMemory[memoryConfigs.Length];

            for (var i = 0; i < memoryConfigs.Length; i++)
            {
                var mc = memoryConfigs[i];
                var unlocked = IsUnlockedByQuest(mc) || LedgerContains(saved, mc.Id);
                memories[i] = new CharacterMemory(mc.Id, config.Id, unlocked, mc.IsGolden,
                    mc.QuestId, mc.QuestChainId);
            }

            return new CharacterModel(config, saved?.Discovered ?? false, memories);
        }

        public CharacterJournalEntry CreateJournalEntry(CharacterConfig config, SavedCharacter saved)
        {
            var memoryConfigs = config.Memories ?? Array.Empty<CharacterMemoryConfig>();
            var rows = new CharacterJournalMemory[memoryConfigs.Length];

            for (var i = 0; i < memoryConfigs.Length; i++)
            {
                var mc = memoryConfigs[i];
                var link = ResolveJournalLink(mc);

                rows[i] = new CharacterJournalMemory
                {
                    MemoryId = mc.Id,
                    Unlocked = link.Unlocked || LedgerContains(saved, mc.Id),
                    IsGolden = mc.IsGolden,
                    TitleKey = mc.TitleKey,
                    DescriptionKey = mc.DescriptionKey,
                    PhotoKey = mc.PhotoKey,
                    LinkedQuestId = link.QuestId,
                    LinkedQuestState = link.State,
                };
            }

            return new CharacterJournalEntry
            {
                CharacterId = config.Id,
                Discovered = saved?.Discovered ?? false,
                DisplayNameKey = config.DisplayNameKey,
                RoleKey = config.RoleKey,
                Memories = rows,
            };
        }

        // ----- quest-derive rules (also used by CharactersService reconcile/events) -----

        public bool IsUnlockedByQuest(CharacterMemoryConfig mc)
        {
            if (!string.IsNullOrEmpty(mc.QuestId))
                return _quests.GetQuestState(mc.QuestId) == QuestState.Awarded;

            if (!string.IsNullOrEmpty(mc.QuestChainId))
                return _quests.GetChain(mc.QuestChainId)?.FinalQuest?.State == QuestState.Awarded;

            return false;
        }

        public bool IsDiscoveredByQuest(CharacterConfig config)
        {
            // Explicit discovery quests/chains + every memory-linked quest/chain. "Started" = state != Pending.
            if (AnyQuestStarted(config.DiscoveryQuestIds)) return true;
            if (AnyChainStarted(config.DiscoveryQuestChainIds)) return true;

            var memories = config.Memories;
            if (memories != null)
            {
                for (var i = 0; i < memories.Length; i++)
                {
                    var mc = memories[i];
                    if (!string.IsNullOrEmpty(mc.QuestId) &&
                        _quests.GetQuestState(mc.QuestId) != QuestState.Pending) return true;
                    if (!string.IsNullOrEmpty(mc.QuestChainId) && ChainStarted(mc.QuestChainId)) return true;
                }
            }

            return false;
        }

        private bool AnyQuestStarted(string[] questIds)
        {
            if (questIds == null) return false;
            for (var i = 0; i < questIds.Length; i++)
                if (!string.IsNullOrEmpty(questIds[i]) &&
                    _quests.GetQuestState(questIds[i]) != QuestState.Pending) return true;
            return false;
        }

        private bool AnyChainStarted(string[] chainIds)
        {
            if (chainIds == null) return false;
            for (var i = 0; i < chainIds.Length; i++)
                if (ChainStarted(chainIds[i])) return true;
            return false;
        }

        private bool ChainStarted(string chainId)
        {
            if (string.IsNullOrEmpty(chainId)) return false;
            var state = _quests.GetChain(chainId)?.CurrentQuest?.State ?? QuestState.Pending;
            return state != QuestState.Pending;
        }

        private static bool LedgerContains(SavedCharacter saved, string memoryId)
            => saved?.UnlockedMemoryIds != null && saved.UnlockedMemoryIds.Contains(memoryId);

        /// <summary>Journal link describes the award quest: for a chain, its FinalQuest (id + state agree).</summary>
        private QuestLink ResolveJournalLink(CharacterMemoryConfig mc)
        {
            if (!string.IsNullOrEmpty(mc.QuestId))
            {
                var state = _quests.GetQuestState(mc.QuestId);
                return new QuestLink(state == QuestState.Awarded, mc.QuestId, state);
            }

            if (!string.IsNullOrEmpty(mc.QuestChainId))
            {
                var final = _quests.GetChain(mc.QuestChainId)?.FinalQuest;
                var state = final?.State ?? QuestState.Pending;
                return new QuestLink(state == QuestState.Awarded, final?.Id, state);
            }

            return new QuestLink(false, null, QuestState.Pending);
        }

        private readonly struct QuestLink
        {
            public QuestLink(bool unlocked, string questId, QuestState state)
            {
                Unlocked = unlocked;
                QuestId = questId;
                State = state;
            }

            public bool Unlocked { get; }
            public string QuestId { get; }
            public QuestState State { get; }
        }
    }
}
