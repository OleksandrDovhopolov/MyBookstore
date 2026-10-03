using System;
using System.Collections.Generic;
using System.Linq;
using Game.Configs.Models;
using Game.Quest.API;
using Game.Quest.UI;
using NUnit.Framework;

namespace Game.Journal.UI.Tests.Editor
{
    /// <summary>
    /// The journal quest list is the only surface that renders quests, so it is also the only place that
    /// can hide the memory-unlock wrappers. Mirrors the HiddenInJournal coverage on the characters tab.
    /// </summary>
    public sealed class QuestViewModelBuilderTests
    {
        [Test]
        public void Build_SkipsPendingAndHiddenQuests()
        {
            var models = new QuestViewModelBuilder().Build(new IQuest[]
            {
                Quest("visible", QuestState.Active),
                Quest("pending", QuestState.Pending),
                Quest("service", QuestState.Awarded, hidden: true),
                Quest("done", QuestState.Awarded)
            });

            CollectionAssert.AreEqual(new[] { "visible", "done" }, models.Select(m => m.Id).ToArray());
        }

        [Test]
        public void Build_HidesServiceQuestInEveryState()
        {
            var states = new[]
            {
                QuestState.Active, QuestState.ReadyToAward, QuestState.Awarded, QuestState.Failed
            };

            foreach (var state in states)
            {
                var models = new QuestViewModelBuilder().Build(new[] { Quest("service", state, hidden: true) });
                CollectionAssert.IsEmpty(models, state.ToString());
            }
        }

        [Test]
        public void Build_KeepsQuestWithoutConfig()
        {
            var models = new QuestViewModelBuilder().Build(new IQuest[] { new QuestStub("bare", QuestState.Active, null) });

            Assert.AreEqual(1, models.Count);
            Assert.AreEqual("bare", models[0].Id);
        }

        private static IQuest Quest(string id, QuestState state, bool hidden = false)
            => new QuestStub(id, state, new QuestConfig { Id = id, Type = "story", HiddenInJournal = hidden });

        private sealed class QuestStub : IQuest
        {
            public QuestStub(string id, QuestState state, QuestConfig config)
            {
                Id = id;
                State = state;
                Config = config;
            }

            public string Id { get; }
            public QuestType Type => QuestType.Story;
            public QuestState State { get; }
            public string ChainId => null;
            public QuestConfig Config { get; }
            public IReadOnlyList<IQuestTask> Tasks => Array.Empty<IQuestTask>();
            public IQuestTask GetTask(int id) => null;
        }
    }
}
