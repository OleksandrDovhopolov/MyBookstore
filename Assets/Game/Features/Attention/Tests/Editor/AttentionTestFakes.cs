using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Attention.API;
using Newtonsoft.Json;
using Save;

namespace Game.Attention.Tests.Editor
{
    /// <summary>In-memory <see cref="ISaveService"/> with JSON round-trip, hook and dirty capture.</summary>
    internal sealed class FakeSaveService : ISaveService
    {
        private readonly Dictionary<string, string> _store = new();

        public List<ISaveHook> RegisteredHooks { get; } = new();
        public int MarkDirtyCount { get; private set; }

        public bool HasModule(string moduleKey) => _store.ContainsKey(moduleKey);

        public void Seed<T>(string moduleKey, T value) =>
            _store[moduleKey] = JsonConvert.SerializeObject(value);

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
        public void MarkDirty() => MarkDirtyCount++;
        public void RegisterHook(ISaveHook hook) { if (hook != null) RegisteredHooks.Add(hook); }
        public IDisposable BlockAutosave() => new NoopLease();
        public void Dispose() { }

        private sealed class NoopLease : IDisposable { public void Dispose() { } }
    }

    internal sealed class FakeAttentionRepository : IAttentionRepository
    {
        public SavedAttention Loaded { get; set; } = new();
        public SavedAttention LastSaved { get; private set; }
        public int SaveCount { get; private set; }

        public UniTask<SavedAttention> LoadAsync(CancellationToken ct) => UniTask.FromResult(Loaded);

        public UniTask SaveAsync(SavedAttention state, CancellationToken ct)
        {
            LastSaved = state;
            SaveCount++;
            return UniTask.CompletedTask;
        }
    }

    internal sealed class FakeAttentionSource : IAttentionSource
    {
        private List<string> _ids;

        public FakeAttentionSource(string key, params string[] ids)
        {
            Key = key;
            _ids = new List<string>(ids ?? Array.Empty<string>());
        }

        public string Key { get; }
        public IEnumerable<string> CurrentIds => _ids;
        public event Action Changed;

        public void SetIds(params string[] ids)
        {
            _ids = new List<string>(ids ?? Array.Empty<string>());
            Changed?.Invoke();
        }

        public void RaiseChanged() => Changed?.Invoke();
    }

    internal sealed class FakeAttentionFlagSource : IAttentionFlagSource
    {
        private bool _hasUnseen;

        public FakeAttentionFlagSource(string key, bool hasUnseen)
        {
            Key = key;
            _hasUnseen = hasUnseen;
        }

        public string Key { get; }
        public bool HasUnseen => _hasUnseen;
        public int MarkSeenCount { get; private set; }
        public event Action Changed;

        public void MarkSeen()
        {
            MarkSeenCount++;
            _hasUnseen = false;
        }

        public void SetHasUnseen(bool value)
        {
            _hasUnseen = value;
            Changed?.Invoke();
        }
    }
}
