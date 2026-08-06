using System.IO;
using Book.Sell.Editor;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor
{
    public sealed class DialogueDeliveredConditionReferenceValidatorTests
    {
        [Test]
        public void Validate_AcceptsKnownDialogueIdInsideComposite()
        {
            using var fixture = new TempConfigs(
                @"[
  {
    ""id"": ""q_intro_eddi"",
    ""tasks"": [
      {
        ""id"": 1,
        ""completionConditions"": { ""all"": [
          { ""type"": ""dialogueDelivered"", ""dialogueId"": ""eddy1"" },
          { ""not"": { ""type"": ""dialogueDelivered"", ""dialogueId"": ""millie1"" } }
        ] }
      }
    ],
    ""activationConditions"": null
  }
]",
                @"[
  { ""id"": ""eddy1"", ""nodes"": [] },
  { ""id"": ""millie1"", ""nodes"": [] }
]");

            var report = DialogueDeliveredConditionReferenceValidator.Validate(fixture.QuestsPath, fixture.DialoguesPath);

            Assert.IsFalse(report.HasErrors);
            Assert.AreEqual(2, report.CheckedReferences);
        }

        [Test]
        public void Validate_ReportsMissingAndUnknownDialogueIds()
        {
            using var fixture = new TempConfigs(
                @"[
  {
    ""id"": ""q_intro_eddi"",
    ""activationConditions"": { ""type"": ""dialogueDelivered"" },
    ""failConditions"": { ""type"": ""dialogueDelivered"", ""dialogueId"": ""missing"" },
    ""tasks"": []
  }
]",
                @"[
  { ""id"": ""eddy1"", ""nodes"": [] }
]");

            var report = DialogueDeliveredConditionReferenceValidator.Validate(fixture.QuestsPath, fixture.DialoguesPath);

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(2, report.Errors.Count);
            Assert.That(report.Errors[0], Does.Contain("without dialogueId"));
            Assert.That(report.Errors[1], Does.Contain("missing dialogueId 'missing'"));
        }

        private sealed class TempConfigs : System.IDisposable
        {
            private readonly string _dir;

            public TempConfigs(string questsJson, string dialoguesJson)
            {
                _dir = Path.Combine(Path.GetTempPath(), "dialogue-delivered-validator-" + System.Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_dir);
                QuestsPath = Path.Combine(_dir, "quests.json");
                DialoguesPath = Path.Combine(_dir, "dialogues.json");
                File.WriteAllText(QuestsPath, questsJson);
                File.WriteAllText(DialoguesPath, dialoguesJson);
            }

            public string QuestsPath { get; }
            public string DialoguesPath { get; }

            public void Dispose()
            {
                if (Directory.Exists(_dir))
                    Directory.Delete(_dir, recursive: true);
            }
        }
    }
}
