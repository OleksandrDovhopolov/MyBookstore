using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    /// the selected preset and runs the real evaluator/scorer, so picking a book shows Excellent/Failed.
    /// </summary>
    public sealed class ActiveSaleCheatModule : ICheatsModule
    {
        private const string CardsGroup = "Active Sale";
        private const string PresetsGroup = "Shelf Preset";
        private const string DefaultPresetId = "preset_default_42";
        private const string LogTag = "[ActiveSaleCheat]";

        private readonly IUIManager _uiManager;
        private readonly IConfigsService _configs;
        private readonly IBookConditionRequestEvaluator _evaluator = new BookConditionRequestEvaluator();

        private ShelfPresetConfig _activePreset;
        private bool _useFullCatalog;

        public ActiveSaleCheatModule(IUIManager uiManager, IConfigsService configs)
        {
            _uiManager = uiManager ?? throw new ArgumentNullException(nameof(uiManager));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        public void Initialize(ICheatsContainer cheatsContainer)
        {
            var presets = _configs.GetAll<ShelfPresetConfig>()
                .Where(preset => preset != null && !string.IsNullOrEmpty(preset.Id))
                .ToArray();

            _activePreset = ResolveDefaultPreset(presets);
            AddPresetButtons(cheatsContainer, presets);

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

                var bookIds = ResolveShelfBookIds();
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

        private void AddPresetButtons(ICheatsContainer cheatsContainer, IReadOnlyList<ShelfPresetConfig> presets)
        {
            cheatsContainer.AddItem<CheatButtonItem>(item =>
                item.OnClick("Use: Full catalog", () =>
                    {
                        _useFullCatalog = true;
                        _activePreset = null;
                        Debug.Log($"{LogTag} Active shelf preset: Full catalog.");
                    })
                    .WithGroup(PresetsGroup));

            if (presets == null || presets.Count == 0)
            {
                Debug.LogWarning($"{LogTag} No shelf presets found; active-sale cheat will use the full catalog.");
                return;
            }

            foreach (var preset in presets)
            {
                var captured = preset;
                var name = DisplayName(captured);

                cheatsContainer.AddItem<CheatButtonItem>(item =>
                    item.OnClick($"Use: {name}", () =>
                        {
                            _useFullCatalog = false;
                            _activePreset = captured;
                            Debug.Log($"{LogTag} Active shelf preset: {name} ({CountBookIds(captured)} book id(s)).");
                        })
                        .WithGroup(PresetsGroup));

                cheatsContainer.AddItem<CheatButtonItem>(item =>
                    item.OnClick($"Check: {name}", () => CheckPreset(captured))
                        .WithGroup(PresetsGroup));
            }
        }

        private ShelfPresetConfig ResolveDefaultPreset(IReadOnlyList<ShelfPresetConfig> presets)
        {
            if (presets == null || presets.Count == 0)
            {
                _useFullCatalog = true;
                return null;
            }

            _useFullCatalog = false;
            return presets.FirstOrDefault(preset =>
                       string.Equals(preset.Id, DefaultPresetId, StringComparison.OrdinalIgnoreCase))
                   ?? presets[0];
        }

        private string[] ResolveShelfBookIds()
        {
            if (_useFullCatalog)
                return FullCatalogBookIds();

            if (_activePreset?.BookIds != null && _activePreset.BookIds.Length > 0)
                return _activePreset.BookIds;

            Debug.LogWarning($"{LogTag} Active shelf preset is missing or empty; using the full catalog.");
            return FullCatalogBookIds();
        }

        private string[] FullCatalogBookIds()
            => _configs.GetAll<BookConfig>()
                .Where(book => book != null && !string.IsNullOrEmpty(book.Id))
                .Select(book => book.Id)
                .ToArray();

        private void CheckPreset(ShelfPresetConfig preset)
        {
            if (preset == null)
            {
                Debug.LogWarning($"{LogTag} Cannot check a null shelf preset.");
                return;
            }

            var requestedIds = preset.BookIds ?? Array.Empty<string>();
            var booksById = _configs.GetAll<BookConfig>()
                .Where(book => book != null && !string.IsNullOrEmpty(book.Id))
                .GroupBy(book => book.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var missingBookIds = requestedIds
                .Where(id => string.IsNullOrWhiteSpace(id) || !booksById.ContainsKey(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var shelfBooks = requestedIds
                .Where(id => !string.IsNullOrWhiteSpace(id) && booksById.ContainsKey(id))
                .Select(id => booksById[id])
                .ToArray();

            var requests = _configs.GetAll<RequestDefinitionConfig>()
                .Where(request => request != null && request.Enabled)
                .ToArray();

            var zeroMatches = new List<string>();
            var sb = new StringBuilder();
            sb.AppendLine($"{LogTag} Shelf preset check: {DisplayName(preset)} ({shelfBooks.Length}/{requestedIds.Length} book id(s), {requests.Length} enabled request(s)).");

            foreach (var request in requests)
            {
                if (!_evaluator.IsValid(request, out var invalidReason))
                {
                    zeroMatches.Add(request.Id);
                    sb.AppendLine($"  {request.Id}: invalid request ({invalidReason})");
                    continue;
                }

                var matchCount = 0;
                for (var i = 0; i < shelfBooks.Length; i++)
                {
                    if (_evaluator.Evaluate(shelfBooks[i], request).IsMatch)
                        matchCount++;
                }

                if (matchCount == 0)
                    zeroMatches.Add(request.Id);

                sb.AppendLine($"  {request.Id}: {matchCount} matching book(s)");
            }

            Debug.Log(sb.ToString().TrimEnd());

            if (missingBookIds.Length > 0)
            {
                Debug.LogWarning(
                    $"{LogTag} Shelf preset '{DisplayName(preset)}' references missing book id(s): " +
                    $"{string.Join(", ", missingBookIds)}");
            }

            if (zeroMatches.Count > 0)
            {
                Debug.LogWarning(
                    $"{LogTag} Shelf preset '{DisplayName(preset)}' has no matching book for request(s): " +
                    $"{string.Join(", ", zeroMatches)}");
            }
        }

        private static string DisplayName(ShelfPresetConfig preset)
            => string.IsNullOrWhiteSpace(preset?.DisplayName) ? preset?.Id ?? "<unnamed>" : preset.DisplayName;

        private static int CountBookIds(ShelfPresetConfig preset)
            => preset?.BookIds?.Length ?? 0;
    }
}
