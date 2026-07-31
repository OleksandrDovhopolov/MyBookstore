using System.IO;
using Game.Configs;
using Game.Configs.Models;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Game.Configs.Tests.Editor
{
    public sealed class RequestDefinitionConfigDeserializationTests
    {
        [TestCase("Assets/Configs/sample_requests.json")]
        [TestCase("Assets/StreamingAssets/Configs/sample_requests.json")]
        public void SampleRequests_DeserializeConditionSchema(string path)
        {
            var json = File.ReadAllText(path);
            var requests = JsonConvert.DeserializeObject<RequestDefinitionConfig[]>(json);

            Assert.IsNotNull(requests);
            Assert.Greater(requests.Length, 0);
            Assert.IsFalse(string.IsNullOrWhiteSpace(requests[0].BookTitle));
            Assert.IsNotNull(requests[0].Conditions);
            Assert.IsNotNull(requests[0].Conditions.All);
            Assert.AreEqual("genres", requests[0].Conditions.All[0].Type);
        }

        [Test]
        public void ConfigsService_LoadsRequestDefinitionsFromSampleRequests()
        {
            var source = new FakeConfigSource
            {
                SampleRequests = @"[
  {
    ""id"": ""sample"",
    ""genre"": ""Travel"",
    ""bookTitle"": ""Sample Book"",
    ""enabled"": true,
    ""conditions"": { ""all"": [ { ""type"": ""genres"", ""operator"": ""contains"", ""value"": ""Travel"" } ] }
  }
]",
                HardRequests = @"[
  {
    ""id"": ""legacy"",
    ""description"": ""Legacy request"",
    ""genre"": ""Crime"",
    ""enabled"": true,
    ""conditions"": { ""all"": [ { ""type"": ""genres"", ""operator"": ""contains"", ""value"": ""Crime"" } ] }
  }
]"
            };
            var service = new ConfigsService(source, overrides: null);
            service.WarmupAsync(System.Threading.CancellationToken.None).GetAwaiter().GetResult();

            var requests = service.GetAll<RequestDefinitionConfig>();

            Assert.AreEqual(1, requests.Count);
            Assert.AreEqual("sample", requests[0].Id);
            Assert.AreEqual("Sample Book", requests[0].BookTitle);
        }

        private sealed class FakeConfigSource : IConfigSource
        {
            public string SampleRequests;
            public string HardRequests;

            public Cysharp.Threading.Tasks.UniTask WarmupAsync(System.Threading.CancellationToken ct)
                => Cysharp.Threading.Tasks.UniTask.CompletedTask;

            public string GetRaw(string fileName)
            {
                if (fileName == "sample_requests") return SampleRequests;
                if (fileName == "hard_requests") return HardRequests;
                return null;
            }
        }
    }
}
