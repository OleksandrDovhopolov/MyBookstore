using System;
using System.Collections.Generic;
using Game.Characters.API;

namespace Game.Journal.UI
{
    public sealed class JournalMemoriesViewModelBuilder
    {
        public IReadOnlyList<JournalMemoryItemModel> Build(
            IEnumerable<ICharacter> characters,
            Func<string, CharacterJournalEntry> getEntry)
        {
            var collected = new List<OrderedMemory>();
            if (characters == null || getEntry == null) return Array.Empty<JournalMemoryItemModel>();

            var sequence = 0;
            foreach (var character in characters)
            {
                if (string.IsNullOrEmpty(character?.Id)) continue;
                var entry = getEntry(character.Id);
                var memories = entry?.Memories;
                if (memories == null) continue;

                for (var i = 0; i < memories.Length; i++)
                {
                    var memory = memories[i];
                    if (memory == null || !memory.Unlocked) continue;
                    collected.Add(new OrderedMemory(
                        new JournalMemoryItemModel(
                            memory.CharacterId,
                            memory.MemoryId,
                            memory.TitleKey,
                            memory.DescriptionKey,
                            memory.PhotoKey,
                            memory.Order,
                            memory.Unlocked,
                            memory.IsGolden,
                            memory.LinkedQuestState),
                        sequence++));
                }
            }

            collected.Sort((a, b) =>
            {
                var order = b.Model.Order.CompareTo(a.Model.Order);
                return order != 0 ? order : a.Sequence.CompareTo(b.Sequence);
            });

            var result = new JournalMemoryItemModel[collected.Count];
            for (var i = 0; i < collected.Count; i++)
                result[i] = collected[i].Model;
            return result;
        }

        private readonly struct OrderedMemory
        {
            public OrderedMemory(JournalMemoryItemModel model, int sequence)
            {
                Model = model;
                Sequence = sequence;
            }

            public JournalMemoryItemModel Model { get; }
            public int Sequence { get; }
        }
    }
}
