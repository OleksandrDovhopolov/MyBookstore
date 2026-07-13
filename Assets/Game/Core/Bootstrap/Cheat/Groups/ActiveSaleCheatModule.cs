using System;
using System.Linq;
using Book.Sell.Domain;
using Book.Sell.Services;
using Book.Sell.UI;
using cheatModule;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.UI;
using UnityEngine;

namespace Game.Cheat
{
    /// <summary>
    /// Debug trigger for the active-sale flow: one button per enabled <see cref="RequestDefinitionConfig"/>
    /// that opens <c>RecommendationMinigameWindow</c> standalone — no sales day, no customer, no interaction
    /// lock. A <see cref="CheatActiveRequestController"/> feeds the window the request + a shelf built from
    /// the whole book catalog and runs the real evaluator/scorer, so picking a book shows Excellent/Failed.
    /// </summary>
    public sealed class ActiveSaleCheatModule : ICheatsModule
    {
        private const string CardsGroup = "Active Sale";
        private const string LogTag = "[ActiveSaleCheat]";

        private readonly IUIManager _uiManager;
        private readonly IConfigsService _configs;
        private readonly IBookConditionRequestEvaluator _evaluator = new BookConditionRequestEvaluator();

        public ActiveSaleCheatModule(IUIManager uiManager, IConfigsService configs)
        {
            _uiManager = uiManager ?? throw new ArgumentNullException(nameof(uiManager));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        public void Initialize(ICheatsContainer cheatsContainer)
        {
            foreach (var cfg in _configs.GetAll<RequestDefinitionConfig>())
            {
                if (cfg == null || string.IsNullOrEmpty(cfg.Id) || !cfg.Enabled) continue;
                var request = cfg;   // capture per-iteration

                cheatsContainer.AddItem<CheatButtonItem>(item =>
                    item.OnClick(request.Id, () => OpenAsync(request).Forget())
                        .WithGroup(CardsGroup));
            }
        }

        private async UniTaskVoid OpenAsync(RequestDefinitionConfig cfg)
        {
            try
            {
                var runtime = ActiveRequestRuntime.FromCondition(cfg, _evaluator.BuildDebugText(cfg));

                // Full catalog on the shelf so any book is pickable for the check.
                var bookIds = _configs.GetAll<BookConfig>().Select(b => b.Id).ToArray();
                var shelf = new SalesShelfBuilder(_configs).Build(bookIds);

                // Pure evaluator/scorer, new-ed directly: the cheat runs outside the sales-day DI scope.
                var controller = new CheatActiveRequestController(
                    runtime, shelf, new ActiveRequestScoringService(_evaluator));

                await _uiManager.ShowAsync<RecommendationMinigameWindow>(new RecommendationMinigameArgs(controller));
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} Failed to open active request '{cfg?.Id}': {ex}");
            }
        }
    }
}
