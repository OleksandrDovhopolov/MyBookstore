using System;
using Book.Sell.API;
using Book.Sell.Domain;
using Game.Configs.Models;
using UnityEngine;

namespace Book.Sell.Services
{
    public sealed class ActiveRequestScoringService : IActiveRequestScoringService
    {
        private const string LogPrefix = "[ActiveRequests]";

        private readonly IBookConditionRequestEvaluator _conditionEvaluator;

        public ActiveRequestScoringService(IBookConditionRequestEvaluator conditionEvaluator)
        {
            _conditionEvaluator = conditionEvaluator ?? throw new ArgumentNullException(nameof(conditionEvaluator));
        }

        public RecommendationResult Score(BookConfig book, ActiveRequestRuntime request, LocationConfig location)
        {
            if (book == null) throw new ArgumentNullException(nameof(book));
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (request.ConditionRequest == null)
            {
                Debug.LogError($"{LogPrefix} request '{request.Id}' has no condition payload; treated as Failed.");
                return new RecommendationResult(
                    request.Id,
                    book.Id,
                    RecommendationTier.Failed,
                    default,
                    RecommendationReason.Empty,
                    0);
            }

            var evaluation = _conditionEvaluator.Evaluate(book, request.ConditionRequest);
            var tier = evaluation.IsMatch ? RecommendationTier.Excellent : RecommendationTier.Failed;
            var gold = evaluation.IsMatch ? BookConfig.FixedPriceGold : 0;
            var breakdown = new ScoreBreakdown(evaluation.IsMatch ? 1 : 0, 0, 0, 0);

            return new RecommendationResult(
                request.Id,
                book.Id,
                tier,
                breakdown,
                RecommendationReason.Empty,
                gold);
        }
    }
}
