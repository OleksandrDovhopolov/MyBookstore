using System.IO;
using Game.Configs.Models;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Game.Configs.Tests.Editor
{
    public sealed class RequestDefinitionConfigDeserializationTests
    {
        [TestCase("Assets/Configs/hard_requests.json")]
        [TestCase("Assets/StreamingAssets/Configs/hard_requests.json")]
        public void HardRequests_DeserializeConditionSchema(string path)
        {
            var json = File.ReadAllText(path);
            var requests = JsonConvert.DeserializeObject<RequestDefinitionConfig[]>(json);

            Assert.IsNotNull(requests);
            Assert.Greater(requests.Length, 0);
            Assert.IsFalse(string.IsNullOrWhiteSpace(requests[0].Description));
            Assert.IsNotNull(requests[0].Conditions);
            Assert.IsNotNull(requests[0].Conditions.All);
            Assert.AreEqual("genres", requests[0].Conditions.All[0].Type);
        }
    }
}
