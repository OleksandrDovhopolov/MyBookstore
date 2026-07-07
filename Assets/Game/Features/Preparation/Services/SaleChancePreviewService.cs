using System;
using System.Threading;
using Book.Sell.Services;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.DayCycle.Day;
using Game.Decor;
using Game.Preparation.Domain;
using Save;
using UnityEngine;

namespace Game.Preparation.Services
{
    public sealed class SaleChancePreviewService : ISaleChancePreviewService
    {
        private readonly IPreparationSessionService _preparationSession;
        private readonly IConfigsService _configs;
        private readonly IDecorPlacementService _decorPlacement;
        private readonly IBaseSaleChanceCalculator _calculator;
        private readonly ISaveService _save;
        private readonly IDayProgressService _dayProgress;

        public SaleChancePreviewService(
            IPreparationSessionService preparationSession,
            IConfigsService configs,
            IDecorPlacementService decorPlacement,
            IBaseSaleChanceCalculator calculator,
            ISaveService save,
            IDayProgressService dayProgress)
        {
            _preparationSession = preparationSession ?? throw new ArgumentNullException(nameof(preparationSession));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _decorPlacement = decorPlacement ?? throw new ArgumentNullException(nameof(decorPlacement));
            _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _dayProgress = dayProgress ?? throw new ArgumentNullException(nameof(dayProgress));
        }

        public async UniTask<int> GetPercentAsync(BookGenre genre, CancellationToken ct)
        {
            var state = await ResolvePreparationStateAsync(ct);
            ct.ThrowIfCancellationRequested();

            var genreKey = genre.ToString();
            var count = 0;
            if (state?.GenreQuantities != null)
                state.GenreQuantities.TryGetValue(genreKey, out count);

            var locationId = state?.LocationId;
            var location = !string.IsNullOrEmpty(locationId)
                ? _configs.Get<LocationConfig>(locationId)
                : null;
            var activeDecorIds = _decorPlacement.GetActiveDecorIds();
            var chance = _calculator.Compute(genreKey, count, location, activeDecorIds);
            var percent = Mathf.RoundToInt((float)Math.Clamp(chance, 0d, 1d) * 100f);
            return Mathf.Clamp(percent, 0, 100);
        }

        private async UniTask<PreparationSessionState> ResolvePreparationStateAsync(CancellationToken ct)
        {
            if (IsCurrentDay(_preparationSession.CurrentState))
                return _preparationSession.CurrentState;

            var saved = await _save.GetModuleAsync<PreparationSessionState>(PreparationSaveKeys.Session, ct);
            if (IsCurrentDay(saved))
                return saved;

            await _preparationSession.GetGenreQuantitiesPreviewAsync(ct);
            return _preparationSession.CurrentState;
        }

        private bool IsCurrentDay(PreparationSessionState state)
            => state != null && state.Day == _dayProgress.Current.CurrentDay;
    }
}
