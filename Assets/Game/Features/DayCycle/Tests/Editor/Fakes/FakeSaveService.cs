using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Save;

namespace Game.DayCycle.Tests.Editor.Fakes
{
    /// <summary>
    /// Фейк ISaveService поверх in-memory словаря с честным JSON round-trip (Newtonsoft),
    /// как в реальном SaveService — чтобы ловить баги «храним ссылку, а не значение».
    /// Backing store можно переиспользовать новым инстансом сервиса для имитации перезапуска.
    /// </summary>
    public sealed class FakeSaveService : ISaveService
    {
        private readonly Dictionary<string, string> _store;

        public FakeSaveService() : this(new Dictionary<string, string>()) { }

        public FakeSaveService(Dictionary<string, string> store) => _store = store;

        /// <summary>Снимок хранилища — для создания «нового» сервиса после перезапуска.</summary>
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
        public IDisposable BlockAutosave() => new NoopLease();
        public void Dispose() { }

        private sealed class NoopLease : IDisposable
        {
            public void Dispose() { }
        }
    }
}
