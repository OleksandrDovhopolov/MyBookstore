using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.DayCycle.Day;
using Game.DayCycle.Morning.Model;

namespace Game.DayCycle.Morning
{
    /// <inheritdoc cref="IMorningSessionService"/>
    public sealed class MorningSessionService : IMorningSessionService
    {
        private readonly IDayProgressService _progress;
        private readonly IMorningContextResolver _resolver;

        private MorningDayContext _current;

        public MorningSessionService(IDayProgressService progress, IMorningContextResolver resolver)
        {
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        public MorningDayContext CurrentContext => _current;

        public async UniTask<MorningDayContext> StartOrResumeAsync(CancellationToken ct)
        {
            var state = await _progress.LoadAsync(ct);

            // Утро — точка входа в день. Фиксируем фазу (важно при первом входе в день
            // после «Следующего дня» из Итогов). Контекст детерминирован от номера дня,
            // поэтому отдельного сохранения контекста не нужно.
            if (state.CurrentPhase != DayPhase.Morning)
                await _progress.SetPhaseAsync(DayPhase.Morning, ct);

            _current = _resolver.Resolve(state.CurrentDay);
            return _current;
        }

        public async UniTask<MorningContinueResult> ContinueToPreparationAsync(CancellationToken ct)
        {
            if (_current == null)
                await StartOrResumeAsync(ct);

            await _progress.SetPhaseAsync(DayPhase.Preparation, ct);

            return new MorningContinueResult
            {
                Day = _current.Day,
                DayId = _current.DayId,
                EventId = _current.EventId,
                WeatherId = _current.WeatherId,
                ActiveModifierIds = _current.ActiveModifierIds,
                DemandGenres = _current.DemandGenres,
                DemandTags = _current.DemandTags,
                TargetLocationIds = _current.TargetLocationIds
            };
        }
    }
}
