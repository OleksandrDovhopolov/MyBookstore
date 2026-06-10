using System.Threading;
using Cysharp.Threading.Tasks;
using Game.DayCycle.Morning.Model;

namespace Game.DayCycle.Morning
{
    /// <summary>
    /// Оркестрирует фазу «Утро»: открывает/восстанавливает утро текущего дня,
    /// держит разрешённый контекст и выполняет переход в Подготовку.
    /// </summary>
    public interface IMorningSessionService
    {
        /// <summary>Разрешённый контекст текущего утра (после <see cref="StartOrResumeAsync"/>).</summary>
        MorningDayContext CurrentContext { get; }

        /// <summary>
        /// Загружает прогресс дня, ставит фазу = Morning, резолвит контекст текущего дня.
        /// Идемпотентно: повторный вызов на том же дне даёт тот же контекст.
        /// </summary>
        UniTask<MorningDayContext> StartOrResumeAsync(CancellationToken ct);

        /// <summary>
        /// Завершает утро: ставит фазу = Preparation, сохраняет и отдаёт payload для Подготовки.
        /// </summary>
        UniTask<MorningContinueResult> ContinueToPreparationAsync(CancellationToken ct);
    }
}
