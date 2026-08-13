using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Save.Sync;
using Save.Storage;
using Save.Storage.Commands;

namespace Save.Tests.Editor
{
    public sealed class SaveGlobalPayloadParserTests
    {
        [Test]
        public void ExtractDataForStorage_WhenDataIsObject_ReturnsSaveJson()
        {
            var response = JObject.FromObject(new
            {
                data = new
                {
                    Meta = new { Revision = 3 },
                    Modules = new
                    {
                        resources = new
                        {
                            Version = 1,
                            Json = new { Gold = 5 }
                        }
                    }
                },
                lastModified = 1754812345678L
            }).ToString();

            var normalized = SaveGlobalPayloadParser.ExtractDataForStorage(response, out var mode);
            var root = JObject.Parse(normalized);

            Assert.That(mode, Is.EqualTo("data-json"));
            Assert.That(root["Meta"]?["Revision"]?.Value<int>(), Is.EqualTo(3));
            Assert.That(root["Modules"]?["resources"], Is.Not.Null);
        }

        [Test]
        public void ExtractDataForStorage_WhenDataIsEscapedString_ReturnsInnerJson()
        {
            const string saveJson = "{\"Meta\":{\"Revision\":4},\"Modules\":{}}";
            var response = JObject.FromObject(new { data = saveJson, lastModified = 123L }).ToString();

            var normalized = SaveGlobalPayloadParser.ExtractDataForStorage(response, out var mode);

            Assert.That(mode, Is.EqualTo("data-string"));
            Assert.That(normalized, Is.EqualTo(saveJson));
        }

        [Test]
        public void ExtractDataForStorage_WhenRawJson_ReturnsRawJson()
        {
            const string raw = "{\"Meta\":{\"Revision\":2},\"Modules\":{}}";

            var normalized = SaveGlobalPayloadParser.ExtractDataForStorage(raw, out var mode);

            Assert.That(mode, Is.EqualTo("raw-json"));
            Assert.That(normalized, Is.EqualTo(raw));
        }

        [Test]
        public void ExtractDataForStorage_WhenBlank_ReturnsBlank()
        {
            var normalized = SaveGlobalPayloadParser.ExtractDataForStorage("", out var mode);

            Assert.That(mode, Is.EqualTo("empty"));
            Assert.That(normalized, Is.EqualTo(""));
        }

        [Test]
        public void BuildRequestBody_SerializesDataAsJsonString()
        {
            const string saveJson = "{\"Meta\":{\"Revision\":7},\"Modules\":{}}";

            var body = PostSaveGlobalCommand.BuildRequestBody("player-1", saveJson);
            var root = JObject.Parse(body);

            Assert.That(root["playerId"]?.Value<string>(), Is.EqualTo("player-1"));
            Assert.That(root["data"]?.Type, Is.EqualTo(JTokenType.String));
            Assert.That(root["data"]?.Value<string>(), Is.EqualTo(saveJson));
        }

        [Test]
        public void ShouldLocalWinOverServer_WhenServerIsDefaultAndLocalHasModules_ReturnsTrue()
        {
            const string local = "{\"Meta\":{\"Revision\":1},\"Modules\":{\"shop\":{\"Version\":1,\"Json\":{}}}}";
            const string serverDefault = "{\"Meta\":{\"Revision\":1},\"Modules\":{}}";

            var shouldPushLocal = SaveSyncBootstrap.ShouldLocalWinOverServer(local, serverDefault);

            Assert.That(shouldPushLocal, Is.True);
        }

        [Test]
        public void ShouldLocalWinOverServer_WhenServerMaterializesResourcesAndInventoryWithoutMetaModules_ReturnsTrue()
        {
            const string local = "{\"Meta\":{\"Revision\":12},\"Modules\":{\"shop\":{\"Version\":1,\"Json\":{\"Level\":3}}}}";
            var serverEnvelope = JObject.FromObject(new
            {
                data = new
                {
                    Resources = new { Energy = 0, Gems = 0, Gold = 0 },
                    Inventory = new
                    {
                        InventoryItems = new { }
                    }
                },
                lastModified = 1770000000000L
            }).ToString();

            var serverData = SaveGlobalPayloadParser.ExtractDataForStorage(serverEnvelope, out var mode);
            var shouldPushLocal = SaveSyncBootstrap.ShouldLocalWinOverServer(local, serverData);

            Assert.That(mode, Is.EqualTo("data-json"));
            Assert.That(JObject.Parse(serverData)["Meta"], Is.Null);
            Assert.That(JObject.Parse(serverData)["Modules"], Is.Null);
            Assert.That(shouldPushLocal, Is.True);
        }

        [Test]
        public void ShouldLocalWinOverServer_WhenServerHasProgress_ReturnsFalse()
        {
            const string local = "{\"Meta\":{\"Revision\":2},\"Modules\":{\"shop\":{\"Version\":1,\"Json\":{}}}}";
            const string server = "{\"Meta\":{\"Revision\":3},\"Modules\":{\"inventory\":{\"Version\":1,\"Json\":{}}}}";

            var shouldPushLocal = SaveSyncBootstrap.ShouldLocalWinOverServer(local, server);

            Assert.That(shouldPushLocal, Is.False);
        }
    }
}
