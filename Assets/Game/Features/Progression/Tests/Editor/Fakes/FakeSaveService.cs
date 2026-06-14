using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Save;

namespace Game.Progression.Tests.Editor.Fakes
{
    public sealed class FakeSaveService : ISaveService
    {
        private readonly Dictionary<string, string> _store = new();
        public List<ISaveHook> RegisteredHooks { get; } = new();

        public Dictionary<string, string> Store => _store;

        public UniTask<T> GetModuleAsync<T>(string moduleKey, CancellationToken ct) where T : class
        {
            if (_store.TryGetValue(moduleKey, out var json) && !string.IsNullOrEmpty(json))
                return UniTask.FromResult(JsonConvert.DeserializeObject<T>(json));
            return UniTask.FromResult<T>(null);
        }

        public UniTask UpdateModuleAsync<T>(string moduleKey, T value, int schemaVersion, CancellationToken ct)
        {
            _store[moduleKey] = JsonConvert.SerializeObject(value, Formatting.None);
            return UniTask.CompletedTask;
        }

        public UniTask LoadAsync(CancellationToken ct) => UniTask.CompletedTask;
        public UniTask SaveAsync(CancellationToken ct, SaveMode mode = SaveMode.Regular) => UniTask.CompletedTask;
        public void MarkDirty() { }
        public void RegisterHook(ISaveHook hook) { if (hook != null) RegisteredHooks.Add(hook); }
        public IDisposable BlockAutosave() => new NoopLease();
        public void Dispose() { }

        private sealed class NoopLease : IDisposable { public void Dispose() { } }
    }
}
