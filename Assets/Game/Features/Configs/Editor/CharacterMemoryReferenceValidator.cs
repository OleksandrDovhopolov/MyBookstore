using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Configs.Models;
using Newtonsoft.Json;

namespace Game.Configs.Editor
{
    /// <summary>
    /// Validates character memory unlock references. Pure: no logs, no dialogs.
    /// </summary>
    public static class CharacterMemoryReferenceValidator
    {
        public const string ConfigsDir = "Assets/Configs";
        private const string CharactersFileName = "characters.json";
        private const string QuestsFileName = "quests.json";

        public static CharacterMemoryReferenceValidationReport Validate(string configsDir = ConfigsDir)
        {
            var report = new CharacterMemoryReferenceValidationReport();

            if (!TryLoad<CharacterConfig>(Path.Combine(configsDir, CharactersFileName), report, out var characters))
                return report;
            if (!TryLoad<QuestConfig>(Path.Combine(configsDir, QuestsFileName), report, out var quests))
                return report;

            var questIds = new HashSet<string>(
                quests.Where(q => !string.IsNullOrWhiteSpace(q?.Id)).Select(q => q.Id),
                StringComparer.Ordinal);
            var chainIds = new HashSet<string>(
                quests.Where(q => !string.IsNullOrWhiteSpace(q?.ChainId)).Select(q => q.ChainId),
                StringComparer.Ordinal);
            var memoryIds = new HashSet<string>(StringComparer.Ordinal);
            var questMemoryRefs = new Dictionary<string, string>(StringComparer.Ordinal);
            var chainMemoryRefs = new Dictionary<string, ReferenceOwner>(StringComparer.Ordinal);

            foreach (var character in characters)
            {
                if (character == null) continue;
                var characterId = string.IsNullOrWhiteSpace(character.Id) ? "<missing-character-id>" : character.Id;
                foreach (var memory in character.Memories ?? Array.Empty<CharacterMemoryConfig>())
                    ValidateMemory(characterId, memory, questIds, chainIds, memoryIds, questMemoryRefs, chainMemoryRefs, report);
            }

            return report;
        }

        private static void ValidateMemory(
            string characterId,
            CharacterMemoryConfig memory,
            HashSet<string> questIds,
            HashSet<string> chainIds,
            HashSet<string> memoryIds,
            Dictionary<string, string> questMemoryRefs,
            Dictionary<string, ReferenceOwner> chainMemoryRefs,
            CharacterMemoryReferenceValidationReport report)
        {
            if (memory == null) return;

            var memoryPath = string.IsNullOrWhiteSpace(memory.Id)
                ? $"character '{characterId}' memory <missing-id>"
                : $"character '{characterId}' memory '{memory.Id}'";

            if (string.IsNullOrWhiteSpace(memory.Id))
                report.Errors.Add($"{memoryPath} has no id.");
            else if (!memoryIds.Add(memory.Id))
                report.Errors.Add($"{memoryPath} duplicates memory id '{memory.Id}'. Memory ids must be globally unique.");

            var hasQuest = !string.IsNullOrWhiteSpace(memory.QuestId);
            var hasChain = !string.IsNullOrWhiteSpace(memory.QuestChainId);
            var sourceCount = (memory.UnlockedAtStart ? 1 : 0) + (hasQuest ? 1 : 0) + (hasChain ? 1 : 0);
            if (sourceCount != 1)
                report.Errors.Add($"{memoryPath} must set exactly one unlock source: unlockedAtStart, questId or questChainId.");

            if (hasQuest)
            {
                if (!questIds.Contains(memory.QuestId))
                    report.Errors.Add($"{memoryPath} references missing questId '{memory.QuestId}'.");
                TrackUniqueReference(questMemoryRefs, memory.QuestId, memoryPath, "questId", report);
            }

            if (hasChain)
            {
                if (!chainIds.Contains(memory.QuestChainId))
                    report.Errors.Add($"{memoryPath} references missing questChainId '{memory.QuestChainId}'.");
                TrackUniqueChainReference(chainMemoryRefs, memory.QuestChainId, characterId, memoryPath, report);
            }
        }

        private static void TrackUniqueReference(
            Dictionary<string, string> references,
            string id,
            string memoryPath,
            string fieldName,
            CharacterMemoryReferenceValidationReport report)
        {
            if (!references.TryGetValue(id, out var previous))
            {
                references[id] = memoryPath;
                return;
            }

            report.Errors.Add($"{memoryPath} reuses {fieldName} '{id}' already used by {previous}.");
        }

        private static void TrackUniqueChainReference(
            Dictionary<string, ReferenceOwner> references,
            string id,
            string characterId,
            string memoryPath,
            CharacterMemoryReferenceValidationReport report)
        {
            if (!references.TryGetValue(id, out var previous))
            {
                references[id] = new ReferenceOwner(characterId, memoryPath);
                return;
            }

            if (string.Equals(previous.CharacterId, characterId, StringComparison.Ordinal))
                return;

            report.Errors.Add($"{memoryPath} reuses questChainId '{id}' already used by {previous.MemoryPath}.");
        }

        private static bool TryLoad<T>(
            string path,
            CharacterMemoryReferenceValidationReport report,
            out List<T> items)
        {
            items = null;

            if (!File.Exists(path))
            {
                report.Errors.Add($"File not found: {path}");
                return false;
            }

            try
            {
                items = JsonConvert.DeserializeObject<List<T>>(File.ReadAllText(path)) ?? new List<T>();
                return true;
            }
            catch (Exception ex)
            {
                report.Errors.Add($"Failed to parse {path}: {ex.Message}");
                return false;
            }
        }

        private readonly struct ReferenceOwner
        {
            public ReferenceOwner(string characterId, string memoryPath)
            {
                CharacterId = characterId;
                MemoryPath = memoryPath;
            }

            public string CharacterId { get; }
            public string MemoryPath { get; }
        }
    }

    public sealed class CharacterMemoryReferenceValidationReport
    {
        public List<string> Errors { get; } = new();
        public bool HasErrors => Errors.Count > 0;
    }
}
