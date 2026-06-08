using System.Threading;
using Cysharp.Threading.Tasks;

namespace Save
{
    // Async hook — вызывается SaveService до/после load и save.
    // Используй для flush незавершённых транзакций перед сохранением.
    // Регистрируй через ISaveService.RegisterHook() в OnBuildCallback.
    public interface ISaveHook
    {
        UniTask BeforeSaveAsync(CancellationToken ct);
        UniTask AfterLoadAsync(CancellationToken ct);
    }
}
