using Game.Tutorial.API;
using Game.Tutorial.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tutorial.Tests.Editor
{
    public sealed class TutorialSettingsTests
    {
        [Test]
        public void MissingSequenceId_IsEnabled()
        {
            var settings = TutorialSettings.CreateWithEntries(
                new TutorialSettings.Entry(TutorialSequenceIds.DayOne, false));

            Assert.IsTrue(settings.IsEnabled(TutorialSequenceIds.Hub));
        }

        [Test]
        public void ConfiguredDisabledSequenceId_IsDisabled()
        {
            var settings = TutorialSettings.CreateWithEntries(
                new TutorialSettings.Entry(TutorialSequenceIds.DayOne, false));

            Assert.IsFalse(settings.IsEnabled(TutorialSequenceIds.DayOne));
        }

        [Test]
        public void DuplicateSequenceId_FirstEntryWins()
        {
            var settings = TutorialSettings.CreateWithEntries(
                new TutorialSettings.Entry(TutorialSequenceIds.DayOne, false),
                new TutorialSettings.Entry(TutorialSequenceIds.DayOne, true));

            Assert.IsFalse(settings.IsEnabled(TutorialSequenceIds.DayOne));
        }

        [Test]
        public void ValidateAgainst_LogsUnknownEmptyAndDuplicateIds()
        {
            var settings = TutorialSettings.CreateWithEntries(
                new TutorialSettings.Entry(string.Empty),
                new TutorialSettings.Entry("unknown_sequence"),
                new TutorialSettings.Entry(TutorialSequenceIds.DayOne),
                new TutorialSettings.Entry(TutorialSequenceIds.DayOne));

            LogAssert.Expect(LogType.Warning, "[Tutorial] tutorial settings contain an empty sequence id.");
            LogAssert.Expect(LogType.Warning, "[Tutorial] tutorial settings contain unknown sequence id 'unknown_sequence'.");
            LogAssert.Expect(LogType.Warning, $"[Tutorial] tutorial settings contain duplicate sequence id '{TutorialSequenceIds.DayOne}'; first entry wins.");

            settings.ValidateAgainst(TutorialSequenceIds.All);
        }
    }
}
