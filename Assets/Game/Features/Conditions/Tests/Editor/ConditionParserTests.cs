using Game.Conditions.API;
using Game.Conditions.Services;
using Game.Conditions.Tests.Editor.Fakes;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;

namespace Game.Conditions.Tests.Editor
{
    public sealed class ConditionParserTests
    {
        private static ConditionParser Build()
        {
            var registry = new ConditionFactoryRegistry(new IConditionFactory[] { new ThresholdConditionFactory() });
            return new ConditionParser(registry);
        }

        private static JObject Threshold(long current, long min)
            => new JObject { ["type"] = ThresholdConditionFactory.TypeId, ["current"] = current, ["min"] = min };

        [Test]
        public void NullNode_AlwaysMet()
        {
            Assert.IsTrue(Build().Parse(null).Evaluate().IsMet);
        }

        [Test]
        public void EmptyNode_AlwaysMet()
        {
            Assert.IsTrue(Build().Parse(new JObject()).Evaluate().IsMet);
        }

        [Test]
        public void Leaf_DispatchedToFactory()
        {
            Assert.IsTrue(Build().Parse(Threshold(30, 30)).Evaluate().IsMet);
            Assert.IsFalse(Build().Parse(Threshold(29, 30)).Evaluate().IsMet);
        }

        [Test]
        public void AllOf_ParsedAndEvaluated()
        {
            var node = new JObject
            {
                ["all"] = new JArray { Threshold(30, 30), Threshold(5, 5) }
            };

            Assert.IsTrue(Build().Parse(node).Evaluate().IsMet);
        }

        [Test]
        public void AllOf_FailsWhenOneLeafUnmet()
        {
            var node = new JObject
            {
                ["all"] = new JArray { Threshold(30, 30), Threshold(4, 5) }
            };

            Assert.IsFalse(Build().Parse(node).Evaluate().IsMet);
        }

        [Test]
        public void Not_Parsed()
        {
            var node = new JObject { ["not"] = Threshold(0, 5) };
            Assert.IsTrue(Build().Parse(node).Evaluate().IsMet);
        }

        [Test]
        public void UnknownType_FailsClosed_AndLogsError()
        {
            LogAssert.Expect(LogType.Error, new Regex("no factory registered for condition type 'mystery'"));

            var node = new JObject { ["type"] = "mystery" };
            Assert.IsFalse(Build().Parse(node).Evaluate().IsMet);
        }

        [Test]
        public void MissingType_FailsClosed_AndLogsError()
        {
            LogAssert.Expect(LogType.Error, new Regex("no composite key and no 'type'"));

            var node = new JObject { ["min"] = 5 };
            Assert.IsFalse(Build().Parse(node).Evaluate().IsMet);
        }
    }
}
