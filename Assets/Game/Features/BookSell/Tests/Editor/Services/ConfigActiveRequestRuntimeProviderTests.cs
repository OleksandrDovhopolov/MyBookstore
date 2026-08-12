using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using Game.Configs.Models;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Book.Sell.Tests.Editor.Services
{
    public sealed class ConfigActiveRequestRuntimeProviderTests
    {
        private static RequestDefinitionConfig Request(string id, bool enabled, string op = "contains") => new()
        {
            Id = id,
            Enabled = enabled,
            Conditions = new RequestConditionGroup
            {
                All = new[]
                {
                    new RequestCondition
                    {
                        Type = "genres",
                        Operator = op,
                        Value = JToken.FromObject("Crime")
                    }
                }
            }
        };

        [Test]
        public void ConditionsMode_ReturnsOnlyEnabledValidRequests()
        {
            LogAssert.Expect(UnityEngine.LogType.Error, "[ActiveRequests] request 'invalid' is invalid and will not spawn: unknown operator 'notAnOperator'.");
            var configs = new FakeConfigsService();
            configs.SetAll(new[]
            {
                Request("valid", true),
                Request("disabled", false),
                Request("invalid", true, "notAnOperator")
            });

            var provider = new ConfigActiveRequestRuntimeProvider(configs, new BookConditionRequestEvaluator());
            var requests = provider.GetRequests();

            Assert.AreEqual(1, requests.Count);
            Assert.AreEqual("valid", requests[0].Id);
            Assert.AreEqual(RequestDifficulty.Unknown, requests[0].Difficulty);
            CollectionAssert.AreEqual(new[] { "Crime" }, requests[0].RequiredGenres);
            StringAssert.Contains("genres", requests[0].Text);
        }
    }
}
