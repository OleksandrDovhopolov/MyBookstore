using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Save
{
    public interface ISaveService : IDisposable
    {
        UniTask LoadAsync(CancellationToken ct);
        UniTask SaveAsync(CancellationToken ct);

        // Returns default(T) when moduleKey not found
        UniTask<T> GetModuleAsync<T>(string moduleKey, CancellationToken ct) where T : class;
        UniTask UpdateModuleAsync<T>(string moduleKey, T value, int schemaVersion, CancellationToken ct);

        void MarkDirty();
        void RegisterHook(ISaveHook hook);
    }
}
