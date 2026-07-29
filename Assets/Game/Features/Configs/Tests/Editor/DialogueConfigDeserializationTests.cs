using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Game.Configs.Tests.Editor
{
    /// <summary>
    /// GAME-6 §Этап 3: the DialogueConfig graph DTO deserializes, and the config wiring
    /// ([ConfigFile("dialogues")] → lazy load → index by Id) resolves through the real ConfigsService.
    /// </summary>
    public sealed class DialogueConfigDeserializationTests
    {
        private static readonly string[] ContentRoots =
        {
            Path.Combine("Assets", "Configs"),
            Path.Combine("Assets", "StreamingAssets", "Configs")
        };

        private const string Json = @"
[
  {
    ""id"": ""dlg_intro_tilde"",
    ""nodes"": [
      { ""nodeId"": ""root"", ""lines"": [ { ""speaker"": ""Тильда"", ""text"": ""Слышал, у вас открылась лавка…"" } ],
        ""options"": [
          { ""text"": ""Заходите!"",        ""next"": ""warm"" },
          { ""text"": ""Мы ещё готовимся."", ""next"": ""cool"" }
        ] },
      { ""nodeId"": ""warm"", ""lines"": [ { ""speaker"": ""Тильда"", ""text"": ""Тогда до встречи."" } ], ""options"": [] },
      { ""nodeId"": ""cool"", ""lines"": [ { ""speaker"": ""Тильда"", ""text"": ""Понимаю, загляну позже."" } ], ""options"": [] }
    ]
  },
  {
    ""id"": ""dlg_quest_01"",
    ""activatesQuestId"": ""q_intro_test"",
    ""nodes"": [
      { ""nodeId"": ""root"", ""lines"": [ { ""speaker"": ""Гость"", ""text"": ""Мне нужна одна книга. Поможете?"" } ],
        ""options"": [ { ""text"": ""Конечно"", ""next"": ""end"" }, { ""text"": ""Позже"", ""next"": ""end"" } ] }
    ]
  }
]";

        [Test]
        public void Deserialize_PopulatesDialogueGraph()
        {
            var dialogues = JsonConvert.DeserializeObject<DialogueConfig[]>(Json);

            Assert.IsNotNull(dialogues);
            Assert.AreEqual(2, dialogues.Length);

            var intro = dialogues[0];
            Assert.AreEqual("dlg_intro_tilde", intro.Id);
            Assert.IsNull(intro.ActivatesQuestId);
            Assert.AreEqual(3, intro.Nodes.Length);

            // Entry node = Nodes[0]. Lines are speaker-tagged objects (GAME-6).
            var root = intro.Nodes[0];
            Assert.AreEqual("root", root.NodeId);
            Assert.AreEqual(1, root.Lines.Length);
            Assert.AreEqual("Тильда", root.Lines[0].Speaker);
            Assert.AreEqual("Слышал, у вас открылась лавка…", root.Lines[0].Text);

            // Branch node: 2 options pointing to sibling nodes.
            Assert.AreEqual(2, root.Options.Length);
            Assert.AreEqual("Заходите!", root.Options[0].Text);
            Assert.AreEqual("warm", root.Options[0].Next);
            Assert.AreEqual("cool", root.Options[1].Next);

            // Terminal node: empty options.
            Assert.AreEqual("warm", intro.Nodes[1].NodeId);
            Assert.IsEmpty(intro.Nodes[1].Options);
        }

        [Test]
        public void Deserialize_SingleNodeDialogue_OptionsEndTheConversation()
        {
            var dialogues = JsonConvert.DeserializeObject<DialogueConfig[]>(Json);
            var quest = dialogues[1];

            Assert.AreEqual("dlg_quest_01", quest.Id);
            Assert.AreEqual("q_intro_test", quest.ActivatesQuestId);
            Assert.AreEqual(1, quest.Nodes.Length);
            Assert.AreEqual(2, quest.Nodes[0].Options.Length);
            Assert.AreEqual("end", quest.Nodes[0].Options[0].Next);
            Assert.AreEqual("end", quest.Nodes[0].Options[1].Next);
        }

        // Wiring guard: proves [ConfigFile("dialogues")] maps to the "dialogues" file, that ConfigsService
        // lazy-loads the type, and indexes it by Id. A broken file mapping would make GetRaw("dialogues")
        // miss the fake and return an empty catalog — caught here, not silently green.
        [Test]
        public void ConfigsService_LoadsDialoguesByConfigFileMapping_AndIndexesById()
        {
            var service = new ConfigsService(new FakeConfigSource(Json), overrides: null);
            service.WarmupAsync(CancellationToken.None).GetAwaiter().GetResult();

            var all = service.GetAll<DialogueConfig>();
            Assert.AreEqual(2, all.Count);

            var intro = service.Get<DialogueConfig>("dlg_intro_tilde");
            Assert.IsNotNull(intro, "Resolved by Id → [ConfigFile] + lazy load + indexing all wired.");
            Assert.AreEqual("root", intro.Nodes[0].NodeId);

            var quest = service.Get<DialogueConfig>("dlg_quest_01");
            Assert.IsNotNull(quest);
            Assert.AreEqual("q_intro_test", quest.ActivatesQuestId);
        }

        [Test]
        public void Content_IntroQuestDialogues_DeclareQuestActivation()
        {
            foreach (var root in ContentRoots)
            {
                var dialogues = JsonConvert.DeserializeObject<DialogueConfig[]>(
                    File.ReadAllText(Path.Combine(root, "dialogues.json")));

                Assert.IsNull(dialogues.Single(d => d.Id == "eddy1").ActivatesQuestId);
                Assert.AreEqual(
                    "q_intro_eddi",
                    dialogues.Single(d => d.Id == "eddy_quest_1").ActivatesQuestId);
                Assert.AreEqual(
                    "q_intro_milly",
                    dialogues.Single(d => d.Id == "milly1").ActivatesQuestId);
            }
        }

        private sealed class FakeConfigSource : IConfigSource
        {
            private readonly string _dialoguesRaw;

            public FakeConfigSource(string dialoguesRaw) => _dialoguesRaw = dialoguesRaw;

            public UniTask WarmupAsync(CancellationToken ct) => UniTask.CompletedTask;

            // Returns raw only for the "dialogues" key — i.e. only when the [ConfigFile] mapping is correct.
            public string GetRaw(string fileName) => fileName == "dialogues" ? _dialoguesRaw : null;
        }
    }
}
