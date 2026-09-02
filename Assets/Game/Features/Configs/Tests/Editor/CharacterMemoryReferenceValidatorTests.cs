using System;
using System.IO;
using Game.Configs.Editor;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Game.Configs.Tests.Editor
{
    public sealed class CharacterMemoryReferenceValidatorTests
    {
        [Test]
        public void Validate_AcceptsValidQuestAndChainAndStartMemories()
        {
            using var fixture = new TempConfigs(
                Characters(
                    Character("eddi", Memory("mem_eddi", questId: "q_eddi")),
                    Character("owner", Memory("mem_owner", unlockedAtStart: true)),
                    Character("harper",
                        Memory("mem_chain_a", chainId: "chain_harper"),
                        Memory("mem_chain_b", chainId: "chain_harper"))),
                Quests(Quest("q_eddi"), Quest("q_chain", chainId: "chain_harper")));

            var report = CharacterMemoryReferenceValidator.Validate(fixture.Dir);

            Assert.IsFalse(report.HasErrors, Errors(report));
        }

        [Test]
        public void Validate_ReportsMemoryWithoutUnlockSource()
        {
            using var fixture = new TempConfigs(
                Characters(Character("eddi", Memory("mem_eddi"))),
                Quests(Quest("q_eddi")));

            var report = CharacterMemoryReferenceValidator.Validate(fixture.Dir);

            Assert.IsTrue(report.HasErrors);
            StringAssert.Contains("must set exactly one unlock source", Errors(report));
        }

        [Test]
        public void Validate_ReportsMemoryWithMultipleUnlockSources()
        {
            using var fixture = new TempConfigs(
                Characters(Character("eddi", Memory("mem_eddi", questId: "q_eddi", unlockedAtStart: true))),
                Quests(Quest("q_eddi")));

            var report = CharacterMemoryReferenceValidator.Validate(fixture.Dir);

            Assert.IsTrue(report.HasErrors);
            StringAssert.Contains("must set exactly one unlock source", Errors(report));
        }

        [Test]
        public void Validate_ReportsDuplicateMemoryId()
        {
            using var fixture = new TempConfigs(
                Characters(
                    Character("eddi", Memory("mem_shared", questId: "q_eddi")),
                    Character("millie", Memory("mem_shared", questId: "q_millie"))),
                Quests(Quest("q_eddi"), Quest("q_millie")));

            var report = CharacterMemoryReferenceValidator.Validate(fixture.Dir);

            Assert.IsTrue(report.HasErrors);
            StringAssert.Contains("duplicates memory id 'mem_shared'", Errors(report));
        }

        [Test]
        public void Validate_ReportsUnknownQuestId()
        {
            using var fixture = new TempConfigs(
                Characters(Character("eddi", Memory("mem_eddi", questId: "missing"))),
                Quests(Quest("q_eddi")));

            var report = CharacterMemoryReferenceValidator.Validate(fixture.Dir);

            Assert.IsTrue(report.HasErrors);
            StringAssert.Contains("missing questId 'missing'", Errors(report));
        }

        [Test]
        public void Validate_ReportsDuplicateQuestIdMemoryReference()
        {
            using var fixture = new TempConfigs(
                Characters(
                    Character("eddi", Memory("mem_eddi", questId: "q_shared")),
                    Character("millie", Memory("mem_millie", questId: "q_shared"))),
                Quests(Quest("q_shared")));

            var report = CharacterMemoryReferenceValidator.Validate(fixture.Dir);

            Assert.IsTrue(report.HasErrors);
            StringAssert.Contains("reuses questId 'q_shared'", Errors(report));
        }

        [Test]
        public void Validate_ReportsUnknownQuestChainId()
        {
            using var fixture = new TempConfigs(
                Characters(Character("eddi", Memory("mem_eddi", chainId: "missing_chain"))),
                Quests(Quest("q_eddi", chainId: "known_chain")));

            var report = CharacterMemoryReferenceValidator.Validate(fixture.Dir);

            Assert.IsTrue(report.HasErrors);
            StringAssert.Contains("missing questChainId 'missing_chain'", Errors(report));
        }

        [Test]
        public void Validate_ReportsDuplicateQuestChainIdAcrossCharacters()
        {
            using var fixture = new TempConfigs(
                Characters(
                    Character("eddi", Memory("mem_eddi", chainId: "shared_chain")),
                    Character("millie", Memory("mem_millie", chainId: "shared_chain"))),
                Quests(Quest("q_chain", chainId: "shared_chain")));

            var report = CharacterMemoryReferenceValidator.Validate(fixture.Dir);

            Assert.IsTrue(report.HasErrors);
            StringAssert.Contains("reuses questChainId 'shared_chain'", Errors(report));
        }

        private static object Character(string id, params object[] memories)
            => new
            {
                id,
                displayNameKey = id,
                memories
            };

        private static object Memory(
            string id,
            string questId = null,
            string chainId = null,
            bool unlockedAtStart = false)
            => new
            {
                id,
                titleKey = $"memory.{id}.title",
                descriptionKey = $"memory.{id}.description",
                photoKey = $"memory_{id}",
                questId,
                questChainId = chainId,
                unlockedAtStart
            };

        private static object Quest(string id, string chainId = null)
            => new { id, chainId, tasks = Array.Empty<object>() };

        private static string Characters(params object[] characters)
            => JsonConvert.SerializeObject(characters);

        private static string Quests(params object[] quests)
            => JsonConvert.SerializeObject(quests);

        private static string Errors(CharacterMemoryReferenceValidationReport report)
            => string.Join(" | ", report.Errors);

        private sealed class TempConfigs : IDisposable
        {
            public TempConfigs(string charactersJson, string questsJson)
            {
                Dir = Path.Combine(Path.GetTempPath(), "character-memory-validator-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Dir);
                File.WriteAllText(Path.Combine(Dir, "characters.json"), charactersJson);
                File.WriteAllText(Path.Combine(Dir, "quests.json"), questsJson);
            }

            public string Dir { get; }

            public void Dispose()
            {
                if (Directory.Exists(Dir))
                    Directory.Delete(Dir, recursive: true);
            }
        }
    }
}
