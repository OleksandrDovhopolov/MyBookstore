using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Save;

namespace Game.DayCycle.Day
{
    /// <inheritdoc cref="IDayProgressService"/>
    public sealed class DayProgressService : IDayProgressService
    {
        public const string ModuleKey = "day_progress";
        public const int SchemaVersion = 1;

        private readonly ISaveService _save;
        private DayProgressState _state;

        public DayProgressService(ISaveService save)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
        }

        public event Action<DayProgressState> PhaseChanged;

        public DayProgressState Current => _state ??= new DayProgressState();

        public async UniTask<DayProgressState> LoadAsync(CancellationToken ct)
        {
            _state = await _save.GetModuleAsync<DayProgressState>(ModuleKey, ct) ?? new DayProgressState();
            return _state;
        }

        public async UniTask SetPhaseAsync(DayPhase phase, CancellationToken ct)
        {
            Current.CurrentPhase = phase;
            await SaveAsync(ct);
            PhaseChanged?.Invoke(Current);
        }

        public async UniTask MarkCurrentDayCompletedAsync(CancellationToken ct)
        {
            var state = Current;
            if (!state.CompletedDays.Contains(state.CurrentDay))
                state.CompletedDays.Add(state.CurrentDay);

            state.CurrentPhase = DayPhase.Results;
            await SaveAsync(ct);
            PhaseChanged?.Invoke(Current);
        }

        public async UniTask AdvanceToNextDayAsync(CancellationToken ct)
        {
            var state = Current;
            if (!state.CompletedDays.Contains(state.CurrentDay))
                state.CompletedDays.Add(state.CurrentDay);

            state.CurrentDay += 1;
            state.CurrentPhase = DayPhase.Morning;
            await SaveAsync(ct);
            PhaseChanged?.Invoke(Current);
        }

        public UniTask SaveAsync(CancellationToken ct)
            => _save.UpdateModuleAsync(ModuleKey, Current, SchemaVersion, ct);
    }
}
