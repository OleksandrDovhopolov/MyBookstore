using System.Threading;
using Cysharp.Threading.Tasks;

namespace Save.Storage
{
    // Exists() намеренно убран (баг Research: HttpSaveStorage.Exists() всегда возвращал true).
    // Признак "нет данных" — LoadAsync возвращает null.
    public interface ISaveStorage
    {
        UniTask SaveAsync(string data, CancellationToken ct);
        UniTask<string> LoadAsync(CancellationToken ct);
        UniTask DeleteAsync(CancellationToken ct);
        UniTask<long> GetLastModifiedTimestampAsync(CancellationToken ct);
    }
}
