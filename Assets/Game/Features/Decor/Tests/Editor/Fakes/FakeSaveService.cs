using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Save;

namespace Game.Decor.Tests.Editor.Fakes
{
    public sealed class FakeSaveService : ISaveService
    {
        private readonly Dictionary<string, string> _store;

        public FakeSaveService() : this(new Dictionary<string, string>()) { }
        public FakeSaveService(Dictionary<string, string> store) => _store = store;

        public Dictionary<string, string> Store => _store;
        public int SaveCallCount { get; private set; }

        public UniTask<T> GetModuleAsync<T>(string moduleKey, CancellationToken ct) where T : class
        {
            if (_store.TryGetValue(moduleKey, out var json) && !string.IsNullOrEmpty(json))
                return UniTask.FromResult(JsonConvert.DeserializeObject<T>(json));
            return UniTask.FromResult<T>(null);
        }

        public UniTask UpdateModuleAsync<T>(string moduleKey, T value, int schemaVersion, CancellationToken ct)
        {
            _store[moduleKey] = JsonConvert.SerializeObject(value, Formatting.None);
            SaveCallCount++;
            return UniTask.CompletedTask;
        }

        public UniTask LoadAsync(CancellationToken ct) => UniTask.CompletedTask;
        public UniTask SaveAsync(CancellationToken ct, SaveMode mode = SaveMode.Regular) => UniTask.CompletedTask;
        public void MarkDirty() { }
        public void RegisterHook(ISaveHook hook) { }
        public IDisposable BlockAutosave() => new Lease();
        public void Dispose() { }

        private sealed class Lease : IDisposable { public void Dispose() { } }
    }
}
