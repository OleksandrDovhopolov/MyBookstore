using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Ftue.Services
{
    /// <summary>
    /// При первом запуске игры заливает стартовое состояние игрока (золото + набор книг
    /// по жанровым ведёркам) и помечает FTUE применённым. Вызывается из Bootstrap pipeline
    /// после загрузки Save и до перехода в GameplayScene.
    /// Идемпотентен: повторный вызов после успешного применения — no-op.
    /// </summary>
    public interface IFtueBootstrapper
    {
        UniTask RunAsync(CancellationToken ct);
    }
}
