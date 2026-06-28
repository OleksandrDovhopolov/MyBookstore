using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Save;

namespace Book.Sell.Tests.Editor.Fakes
{
    public sealed class FakeSaveService : ISaveService
    {
        private readonly Dictionary<string, object> _modules = new();

        public UniTask LoadAsync(CancellationToken ct) => UniTask.CompletedTask;

        public UniTask SaveAsync(CancellationToken ct, SaveMode mode = SaveMode.Regular) => UniTask.CompletedTask;

        public UniTask<T> GetModuleAsync<T>(string moduleKey, CancellationToken ct) where T : class
            => UniTask.FromResult(_modules.TryGetValue(moduleKey, out var value) ? value as T : null);

        public UniTask UpdateModuleAsync<T>(string moduleKey, T value, int schemaVersion, CancellationToken ct)
        {
            _modules[moduleKey] = value;
            return UniTask.CompletedTask;
        }

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
