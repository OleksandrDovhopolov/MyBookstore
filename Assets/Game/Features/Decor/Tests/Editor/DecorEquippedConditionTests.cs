using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Conditions.API;
using Game.Conditions.Services;
using Game.Decor;
using Game.Decor.Conditions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Decor.Tests.Editor
{
    /// <summary>
    /// "decorEquipped" condition parsed by the engine → evaluated against a fake placement seam.
    /// </summary>
    public sealed class DecorEquippedConditionTests
    {
        private const string Fireplace = "fireplace";

        private static IConditionParser Parser(FakeDecorPlacementService decor)
            => new ConditionParser(new ConditionFactoryRegistry(new IConditionFactory[]
            {
                new DecorEquippedConditionFactory(decor)
            }));

        private static JObject Node(string decorId)
            => new JObject { ["type"] = DecorEquippedConditionFactory.TypeId, ["decorId"] = decorId };

        [Test]
        public void NotMet_WhenDecorNotEquipped()
        {
            var decor = new FakeDecorPlacementService();
            var condition = Parser(decor).Parse(Node(Fireplace));

            var result = condition.Evaluate();
            Assert.IsFalse(result.IsMet);
            Assert.AreEqual($"decorEquipped.{Fireplace}", result.ReasonKey);
        }

        [Test]
        public void Met_WhenDecorEquipped()
        {
            var decor = new FakeDecorPlacementService { Active = { Fireplace } };
            var condition = Parser(decor).Parse(Node(Fireplace));

            Assert.IsTrue(condition.Evaluate().IsMet);
        }

        [Test]
        public void EmptyDecorId_FailsClosed()
        {
            var decor = new FakeDecorPlacementService { Active = { Fireplace } };
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex("factory 'decorEquipped' failed"));

            var node = new JObject { ["type"] = DecorEquippedConditionFactory.TypeId, ["decorId"] = "" };
            Assert.IsFalse(Parser(decor).Parse(node).Evaluate().IsMet);
        }

        private sealed class FakeDecorPlacementService : IDecorPlacementService
        {
            public readonly List<string> Active = new();

            public IReadOnlyList<string> GetActiveDecorIds() => Active;

            public IReadOnlyList<DecorPlacementEntry> GetAllPlacements() => Array.Empty<DecorPlacementEntry>();
            public string GetDecorInSlot(string slotId) => null;
            public UniTask<DecorPlacementResult> PlaceAsync(string decorId, string slotId, CancellationToken ct) => default;
            public UniTask UnplaceAsync(string slotId, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask ClearAllAsync(CancellationToken ct) => UniTask.CompletedTask;

#pragma warning disable CS0067
            public event Action PlacementChanged;
#pragma warning restore CS0067
        }
    }
}
