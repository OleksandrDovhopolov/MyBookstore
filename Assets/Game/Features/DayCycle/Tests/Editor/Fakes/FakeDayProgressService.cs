using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.DayCycle.Day;

namespace Game.DayCycle.Tests.Editor.Fakes
{
    /// <summary>In-memory IDayProgressService for tests — no save round-trip, just direct mutation.</summary>
    public sealed class FakeDayProgressService : IDayProgressService
    {
        public event Action<DayProgressState> PhaseChanged;

        public DayProgressState State { get; } = new();

        public DayProgressState Current => State;

        public int SaveCallCount { get; private set; }
        public int AdvanceCallCount { get; private set; }
        public int MarkCompletedCallCount { get; private set; }

        public UniTask<DayProgressState> LoadAsync(CancellationToken ct) => UniTask.FromResult(State);

        public UniTask SetPhaseAsync(DayPhase phase, CancellationToken ct)
        {
            State.CurrentPhase = phase;
            SaveCallCount++;
            PhaseChanged?.Invoke(State);
            return UniTask.CompletedTask;
        }

        public UniTask MarkCurrentDayCompletedAsync(CancellationToken ct)
        {
            if (!State.CompletedDays.Contains(State.CurrentDay))
                State.CompletedDays.Add(State.CurrentDay);

            State.CurrentPhase = DayPhase.Results;
            MarkCompletedCallCount++;
            SaveCallCount++;
            PhaseChanged?.Invoke(State);
            return UniTask.CompletedTask;
        }

        public UniTask AdvanceToNextDayAsync(CancellationToken ct)
        {
            if (!State.CompletedDays.Contains(State.CurrentDay))
                State.CompletedDays.Add(State.CurrentDay);
            State.CurrentDay += 1;
            State.CurrentPhase = DayPhase.Morning;
            AdvanceCallCount++;
            PhaseChanged?.Invoke(State);
            return UniTask.CompletedTask;
        }

        public UniTask SaveAsync(CancellationToken ct)
        {
            SaveCallCount++;
            return UniTask.CompletedTask;
        }
    }
}
