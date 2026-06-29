using System;
using System.Collections.Generic;
using Game.Characters.API;
using Game.Characters.Services.Persistence;
using Game.Configs.Models;
using Game.Quest.API;

namespace Game.Characters.Services
{
    /// <summary>
    /// Derives runtime character state from quest state (docs/CHARACTER_SYSTEM.md §6/§11):
    /// <list type="bullet">
    /// <item>memory by QuestId → unlocked when that quest is Awarded.</item>
    /// <item>memory by QuestChainId → unlocked when the chain's final quest is Awarded.</item>
    /// <item>discovered → saved flag OR any owning (memory-linked) quest progressed past Pending.</item>
    /// </list>
    /// Unknown quest/chain resolves to Pending → locked, never throws.
    /// </summary>
    internal sealed class CharacterModelFactory : ICharacterModelFactory
    {
        private readonly IQuestsService _quests;

        public CharacterModelFactory(IQuestsService quests)
            => _quests = quests ?? throw new ArgumentNullException(nameof(quests));

        public CharacterModel Create(CharacterConfig config, SavedCharacter saved)
        {
            var memories = BuildMemories(config, out var anyStarted);
            var discovered = IsDiscovered(saved, anyStarted);
            return new CharacterModel(config, discovered, memories);
        }

        public CharacterJournalEntry CreateJournalEntry(CharacterConfig config, SavedCharacter saved)
        {
            var memoryConfigs = config.Memories ?? Array.Empty<CharacterMemoryConfig>();
            var rows = new CharacterJournalMemory[memoryConfigs.Length];
            var anyStarted = false;

            for (var i = 0; i < memoryConfigs.Length; i++)
            {
                var mc = memoryConfigs[i];
                var link = ResolveLink(mc);
                if (link.State != QuestState.Pending) anyStarted = true;

                rows[i] = new CharacterJournalMemory
                {
                    MemoryId = mc.Id,
                    Unlocked = link.Unlocked,
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
                Discovered = IsDiscovered(saved, anyStarted),
                DisplayNameKey = config.DisplayNameKey,
                RoleKey = config.RoleKey,
                Memories = rows,
            };
        }

        private IReadOnlyList<ICharacterMemory> BuildMemories(CharacterConfig config, out bool anyStarted)
        {
            var memoryConfigs = config.Memories ?? Array.Empty<CharacterMemoryConfig>();
            var result = new ICharacterMemory[memoryConfigs.Length];
            anyStarted = false;

            for (var i = 0; i < memoryConfigs.Length; i++)
            {
                var mc = memoryConfigs[i];
                var link = ResolveLink(mc);
                if (link.State != QuestState.Pending) anyStarted = true;

                result[i] = new CharacterMemory(mc.Id, config.Id, link.Unlocked, mc.IsGolden,
                    mc.QuestId, mc.QuestChainId);
            }

            return result;
        }

        private static bool IsDiscovered(SavedCharacter saved, bool anyOwningQuestStarted)
            => (saved != null && saved.Discovered) || anyOwningQuestStarted;

        /// <summary>
        /// Resolves a memory's linked quest into (unlocked, linked-quest-id, current state). For a chain,
        /// the award quest is the final one; discovery uses the chain's current quest.
        /// </summary>
        private QuestLink ResolveLink(CharacterMemoryConfig mc)
        {
            if (!string.IsNullOrEmpty(mc.QuestId))
            {
                var state = _quests.GetQuestState(mc.QuestId);
                return new QuestLink(state == QuestState.Awarded, mc.QuestId, state);
            }

            if (!string.IsNullOrEmpty(mc.QuestChainId))
            {
                var chain = _quests.GetChain(mc.QuestChainId);
                if (chain == null) return new QuestLink(false, null, QuestState.Pending);

                var final = chain.FinalQuest;
                var unlocked = final != null && final.State == QuestState.Awarded;
                // Discovery signal: a chain is "started" once its current quest is past Pending.
                var state = chain.CurrentQuest?.State ?? QuestState.Pending;
                return new QuestLink(unlocked, final?.Id, state);
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
