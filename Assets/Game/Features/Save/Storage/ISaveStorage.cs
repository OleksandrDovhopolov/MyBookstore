using System.Threading;
using Cysharp.Threading.Tasks;

namespace Save.Storage
{
    // Признак "нет данных" — LoadAsync возвращает null. Отдельного Exists() нет намеренно:
    // у HTTP-источника проверка существования = тот же сетевой запрос, что и загрузка.
    public interface ISaveStorage
    {
        UniTask SaveAsync(string data, CancellationToken ct);
        UniTask<string> LoadAsync(CancellationToken ct);
        UniTask DeleteAsync(CancellationToken ct);
        UniTask<long> GetLastModifiedTimestampAsync(CancellationToken ct);
    }
}
