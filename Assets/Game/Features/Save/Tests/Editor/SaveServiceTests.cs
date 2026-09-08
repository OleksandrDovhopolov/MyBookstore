using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Save.Storage;
using UnityEngine;
using UnityEngine.TestTools;

namespace Save.Tests.Editor
{
    public sealed class SaveServiceTests
    {
        [Test]
        public async Task GetModuleAsync_WhenPayloadCannotDeserialize_ReturnsNull()
        {
            const string saveJson =
                "{\"Meta\":{\"SchemaVersion\":1,\"Revision\":1},\"Modules\":{\"broken\":{\"Version\":7,\"Json\":{\"Count\":{\"bad\":true}}}}}";
            using var save = new SaveService(new MemorySaveStorage(saveJson));

            LogAssert.Expect(
                LogType.Warning,
                new Regex(@"\[SaveService\] Module 'broken' v7 cannot be deserialized as StrictDto; returning default\."));

            var loaded = await save.GetModuleAsync<StrictDto>("broken", CancellationToken.None);

            Assert.That(loaded, Is.Null);
        }

        [Test]
        public async Task GetModuleAsync_WhenOneModuleIsBroken_OtherModulesStillLoad()
        {
            const string saveJson =
                "{\"Meta\":{\"SchemaVersion\":1,\"Revision\":1},\"Modules\":{\"broken\":{\"Version\":2,\"Json\":{\"Count\":{\"bad\":true}}},\"valid\":{\"Version\":1,\"Json\":{\"Name\":\"ok\",\"Count\":3}}}}";
            using var save = new SaveService(new MemorySaveStorage(saveJson));

            LogAssert.Expect(
                LogType.Warning,
                new Regex(@"\[SaveService\] Module 'broken' v2 cannot be deserialized as StrictDto; returning default\."));

            var broken = await save.GetModuleAsync<StrictDto>("broken", CancellationToken.None);
            var valid = await save.GetModuleAsync<StrictDto>("valid", CancellationToken.None);

            Assert.That(broken, Is.Null);
            Assert.That(valid, Is.Not.Null);
            Assert.That(valid.Name, Is.EqualTo("ok"));
            Assert.That(valid.Count, Is.EqualTo(3));
        }

        [Test]
        public async Task GetModuleAsync_AfterUpdateModuleAsync_ReadsStructuredPayload()
        {
            var storage = new MemorySaveStorage(null);
            using var save = new SaveService(storage);
            using var autosaveBlock = save.BlockAutosave();
            var dto = new StrictDto { Name = "saved", Count = 42 };

            await save.UpdateModuleAsync("valid", dto, 1, CancellationToken.None);
            var loaded = await save.GetModuleAsync<StrictDto>("valid", CancellationToken.None);

            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.Name, Is.EqualTo("saved"));
            Assert.That(loaded.Count, Is.EqualTo(42));
            Assert.That(storage.SavedData, Is.Null);
        }

        private sealed class StrictDto
        {
            public string Name { get; set; }
            public int Count { get; set; }
        }

        private sealed class MemorySaveStorage : ISaveStorage
        {
            private string _data;

            public MemorySaveStorage(string data)
            {
                _data = data;
            }

            public string SavedData { get; private set; }

            public UniTask SaveAsync(string data, CancellationToken ct)
            {
                SavedData = data;
                _data = data;
                return UniTask.CompletedTask;
            }

            public UniTask<string> LoadAsync(CancellationToken ct) => UniTask.FromResult(_data);

            public UniTask DeleteAsync(CancellationToken ct)
            {
                _data = null;
                SavedData = null;
                return UniTask.CompletedTask;
            }

            public UniTask<long> GetLastModifiedTimestampAsync(CancellationToken ct) => UniTask.FromResult(0L);
        }
    }
}
