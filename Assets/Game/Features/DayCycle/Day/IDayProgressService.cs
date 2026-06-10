using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.DayCycle.Day
{
    /// <summary>
    /// Доступ к общему прогрессу дня (день/фаза/золото/репутация) поверх ISaveService.
    /// Используется всеми фазами core loop. Загружается один раз, дальше живёт в памяти
    /// и пишется в Save при изменениях.
    /// </summary>
    public interface IDayProgressService
    {
        /// <summary>Текущее состояние (после <see cref="LoadAsync"/>). До загрузки — дефолт (день 1, утро).</summary>
        DayProgressState Current { get; }

        /// <summary>Читает прогресс из Save; если модуля нет — создаёт дефолтный (день 1, фаза Morning).</summary>
        UniTask<DayProgressState> LoadAsync(CancellationToken ct);

        /// <summary>Устанавливает фазу и сохраняет.</summary>
        UniTask SetPhaseAsync(DayPhase phase, CancellationToken ct);

        /// <summary>Завершает цикл дня: помечает день завершённым, переходит на день+1 в фазу Morning, сохраняет.</summary>
        UniTask AdvanceToNextDayAsync(CancellationToken ct);

        /// <summary>Принудительно пишет текущее состояние в Save.</summary>
        UniTask SaveAsync(CancellationToken ct);
    }
}
