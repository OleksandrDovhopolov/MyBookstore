using Dialogue;
using Game.Configs.Models;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor
{
    /// <summary>
    /// GAME-6 §Этап 5 A1: the pure dialogue runner. Graph walk (linear + branch), single-node end, and the
    /// unknown-target content-error path. No Unity — the engine returns a status, the view logs/decides.
    /// </summary>
    public sealed class DialogueEngineTests
    {
        private static DialogueOptionConfig Opt(string text, string next)
            => new() { Text = text, Next = next };

        private static DialogueNodeConfig Node(string id, string[] lines, params DialogueOptionConfig[] options)
            => new() { NodeId = id, Lines = ToLines(lines), Options = options };

        private static DialogueLineConfig[] ToLines(string[] texts)
        {
            if (texts == null) return System.Array.Empty<DialogueLineConfig>();
            var result = new DialogueLineConfig[texts.Length];
            for (var i = 0; i < texts.Length; i++)
                result[i] = new DialogueLineConfig { Speaker = "x", Text = texts[i] };
            return result;
        }

        private static DialogueConfig Config(string id, params DialogueNodeConfig[] nodes)
            => new() { Id = id, Nodes = nodes };

        // root -> (warm | cool), both terminal. Mirrors dlg_intro_tilde.
        private static DialogueConfig BranchGraph() => Config(
            "dlg_intro_tilde",
            Node("root", new[] { "Слышал, у вас открылась лавка…" },
                Opt("Заходите!", "warm"),
                Opt("Мы ещё готовимся.", "cool")),
            Node("warm", new[] { "Тогда до встречи." }),
            Node("cool", new[] { "Понимаю, загляну позже." }));

        [Test]
        public void StartsAtFirstNode()
        {
            var engine = new DialogueEngine(BranchGraph());

            Assert.AreEqual("root", engine.Current.NodeId);
            Assert.IsFalse(engine.IsTerminal, "Root has options — not terminal.");
        }

        [Test]
        public void Choose_Branch_WarmOption_AdvancesToTerminalNode()
        {
            var engine = new DialogueEngine(BranchGraph());

            var result = engine.Choose(0);

            Assert.AreEqual(ChooseResult.Advanced, result);
            Assert.AreEqual("warm", engine.Current.NodeId);
            Assert.IsTrue(engine.IsTerminal, "warm has no options — the conversation ends here.");
        }

        [Test]
        public void Choose_Branch_CoolOption_AdvancesToOtherTerminalNode()
        {
            var engine = new DialogueEngine(BranchGraph());

            var result = engine.Choose(1);

            Assert.AreEqual(ChooseResult.Advanced, result);
            Assert.AreEqual("cool", engine.Current.NodeId);
            Assert.IsTrue(engine.IsTerminal);
        }

        // A linear graph: root -> mid -> end. Walk it node by node to the terminal.
        [Test]
        public void Choose_Linear_WalksRootToEnd()
        {
            var engine = new DialogueEngine(Config(
                "linear",
                Node("root", new[] { "a" }, Opt("next", "mid")),
                Node("mid", new[] { "b" }, Opt("finish", "end"))));

            Assert.AreEqual(ChooseResult.Advanced, engine.Choose(0));
            Assert.AreEqual("mid", engine.Current.NodeId);

            Assert.AreEqual(ChooseResult.Ended, engine.Choose(0), "next:'end' ends the conversation.");
            Assert.AreEqual("mid", engine.Current.NodeId, "Ended does not move the cursor.");
        }

        // Single node whose options both point at "end". Mirrors dlg_quest_01.
        // Dummy choice (dlg_tilde_meet): two options both point at the same node — either pick converges to
        // the same continuation, so the dialogue plays identically regardless of the answer.
        [Test]
        public void Choose_ConvergingOptions_BothAdvanceToSameNode()
        {
            DialogueConfig Graph() => Config(
                "converge",
                Node("root", new[] { "a" },
                    Opt("вариант A", "after"),
                    Opt("вариант B", "after")),
                Node("after", new[] { "b" }));

            var a = new DialogueEngine(Graph());
            Assert.AreEqual(ChooseResult.Advanced, a.Choose(0));
            Assert.AreEqual("after", a.Current.NodeId);

            var b = new DialogueEngine(Graph());
            Assert.AreEqual(ChooseResult.Advanced, b.Choose(1));
            Assert.AreEqual("after", b.Current.NodeId);
        }

        [Test]
        public void Choose_SingleNode_OptionsEndConversation()
        {
            var engine = new DialogueEngine(Config(
                "dlg_quest_01",
                Node("root", new[] { "Мне нужна одна книга. Поможете?" },
                    Opt("Конечно", "end"),
                    Opt("Позже", "end"))));

            Assert.IsFalse(engine.IsTerminal, "Has options, so not terminal — but both end.");
            Assert.AreEqual(ChooseResult.Ended, engine.Choose(0));
            Assert.AreEqual(ChooseResult.Ended, engine.Choose(1));
        }

        [Test]
        public void Choose_EmptyNext_EndsConversation()
        {
            var engine = new DialogueEngine(Config(
                "empty_next",
                Node("root", new[] { "a" }, Opt("bye", ""))));

            Assert.AreEqual(ChooseResult.Ended, engine.Choose(0));
        }

        [Test]
        public void Choose_UnknownNext_ReturnsUnknownTarget_DoesNotMove()
        {
            var engine = new DialogueEngine(Config(
                "broken",
                Node("root", new[] { "a" }, Opt("go", "nope"))));

            var result = engine.Choose(0);

            Assert.AreEqual(ChooseResult.UnknownTarget, result);
            Assert.AreEqual("root", engine.Current.NodeId, "Unknown target must not advance the engine.");
        }

        [Test]
        public void Choose_OutOfRangeIndex_ReturnsUnknownTarget()
        {
            var engine = new DialogueEngine(BranchGraph());

            Assert.AreEqual(ChooseResult.UnknownTarget, engine.Choose(5));
            Assert.AreEqual(ChooseResult.UnknownTarget, engine.Choose(-1));
            Assert.AreEqual("root", engine.Current.NodeId);
        }
    }
}
