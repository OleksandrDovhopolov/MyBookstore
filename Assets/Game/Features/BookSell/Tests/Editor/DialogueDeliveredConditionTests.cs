using System;
using System.Collections.Generic;
using System.Threading;
using Book.Sell.API;
using Book.Sell.Conditions;
using Cysharp.Threading.Tasks;
using Game.Conditions.API;
using Game.Conditions.Services;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Book.Sell.Tests.Editor
{
    public sealed class DialogueDeliveredConditionTests
    {
        [Test]
        public void Evaluate_NotDelivered_ReturnsUnmetProgress()
        {
            var delivered = new FakeDeliveredDialogues();
            var condition = new DialogueDeliveredCondition(delivered, "eddy1");

            var result = condition.Evaluate();

            Assert.IsFalse(result.IsMet);
            Assert.AreEqual(0, result.Current);
            Assert.AreEqual(1, result.Target);
            Assert.AreEqual("dialogueDelivered.eddy1", result.ReasonKey);
        }

        [Test]
        public void Evaluate_CommittedDelivered_ReturnsMetProgress()
        {
            var delivered = new FakeDeliveredDialogues();
            delivered.MarkDeliveredAsync("eddy1", CancellationToken.None).GetAwaiter().GetResult();
            var condition = new DialogueDeliveredCondition(delivered, "eddy1");

            var result = condition.Evaluate();

            Assert.IsTrue(result.IsMet);
            Assert.AreEqual(1, result.Current);
            Assert.AreEqual(1, result.Target);
        }

        [Test]
        public void Evaluate_DeferredDelivered_ReturnsMetBeforeCommit()
        {
            var delivered = new FakeDeliveredDialogues();
            delivered.MarkDeliveredDeferredAsync("millie1", CancellationToken.None).GetAwaiter().GetResult();
            var condition = new DialogueDeliveredCondition(delivered, "millie1");

            Assert.IsTrue(condition.Evaluate().IsMet);
        }

        [Test]
        public void Factory_Throws_OnMissingDialogueId()
        {
            var factory = new DialogueDeliveredConditionFactory(new FakeDeliveredDialogues());

            Assert.Throws<ArgumentException>(
                () => factory.Create(new JObject { ["type"] = DialogueDeliveredConditionFactory.TypeId }));
        }

        [Test]
        public void Parser_FailCloses_WhenFactoryThrows()
        {
            var parser = new ConditionParser(new ConditionFactoryRegistry(new IConditionFactory[]
            {
                new DialogueDeliveredConditionFactory(new FakeDeliveredDialogues())
            }));

            LogAssert.Expect(
                LogType.Error,
                "[Conditions] factory 'dialogueDelivered' failed to build condition: missing 'dialogueId'; treated as never-met.");

            var condition = parser.Parse(new JObject { ["type"] = DialogueDeliveredConditionFactory.TypeId });
            var result = condition.Evaluate();

            Assert.IsFalse(result.IsMet);
            Assert.AreEqual("invalid.dialogueDelivered", result.ReasonKey);
        }

        [Test]
        public void Factory_ForwardsDeliveredChangeSource()
        {
            var delivered = new FakeDeliveredDialogues();
            var factory = new DialogueDeliveredConditionFactory(delivered);
            var changed = 0;

            ((IConditionChangeSource)factory).Changed += () => changed++;
            delivered.MarkDeliveredDeferredAsync("eddy1", CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(1, changed);
        }

        private sealed class FakeDeliveredDialogues : IDeliveredDialoguesService
        {
            private readonly HashSet<string> _committed = new(StringComparer.Ordinal);
            private readonly HashSet<string> _pending = new(StringComparer.Ordinal);

            public event Action Changed;

            public bool IsDelivered(string dialogueId)
                => dialogueId != null && (_committed.Contains(dialogueId) || _pending.Contains(dialogueId));

            public UniTask MarkDeliveredAsync(string dialogueId, CancellationToken ct)
            {
                if (!string.IsNullOrWhiteSpace(dialogueId))
                {
                    _committed.Add(dialogueId);
                    Changed?.Invoke();
                }

                return UniTask.CompletedTask;
            }

            public UniTask MarkDeliveredDeferredAsync(string dialogueId, CancellationToken ct)
            {
                if (!string.IsNullOrWhiteSpace(dialogueId))
                {
                    _pending.Add(dialogueId);
                    Changed?.Invoke();
                }

                return UniTask.CompletedTask;
            }

            public UniTask CommitAsync(CancellationToken ct)
            {
                foreach (var id in _pending)
                    _committed.Add(id);

                _pending.Clear();
                Changed?.Invoke();
                return UniTask.CompletedTask;
            }

            public void DiscardDeferred()
            {
                _pending.Clear();
                Changed?.Invoke();
            }
        }
    }
}
