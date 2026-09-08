using System.Collections.Generic;
using Book.Sell.Domain;
using Game.Configs;
using Game.Configs.Models;
using UnityEngine;
using VContainer;

namespace Book.Sell.Services
{
    public sealed class ConfigActiveRequestRuntimeProvider : IActiveRequestRuntimeProvider
    {
        private const string LogPrefix = "[ActiveRequests]";

        private readonly IConfigsService _configs;
        private readonly IBookConditionRequestEvaluator _conditionEvaluator;
        private readonly IActiveRequestGenreResolver _genreResolver;
        private readonly IActiveRequestTextComposer _textComposer;

        public ConfigActiveRequestRuntimeProvider(
            IConfigsService configs,
            IBookConditionRequestEvaluator conditionEvaluator)
            : this(configs, conditionEvaluator, new ConditionActiveRequestGenreResolver(), null)
        {
        }

        public ConfigActiveRequestRuntimeProvider(
            IConfigsService configs,
            IBookConditionRequestEvaluator conditionEvaluator,
            IActiveRequestGenreResolver genreResolver)
            : this(configs, conditionEvaluator, genreResolver, null)
        {
        }

        [Inject]
        public ConfigActiveRequestRuntimeProvider(
            IConfigsService configs,
            IBookConditionRequestEvaluator conditionEvaluator,
            IActiveRequestGenreResolver genreResolver,
            IActiveRequestTextComposer textComposer)
        {
            _configs = configs ?? throw new System.ArgumentNullException(nameof(configs));
            _conditionEvaluator = conditionEvaluator ?? throw new System.ArgumentNullException(nameof(conditionEvaluator));
            _genreResolver = genreResolver ?? throw new System.ArgumentNullException(nameof(genreResolver));
            _textComposer = textComposer ?? new LexiconActiveRequestTextComposer(configs, conditionEvaluator, null);
        }

        public IReadOnlyList<ActiveRequestRuntime> GetRequests()
        {
            var configs = _configs.GetAll<RequestDefinitionConfig>();
            var requests = new List<ActiveRequestRuntime>(configs.Count);
            for (var i = 0; i < configs.Count; i++)
            {
                var request = configs[i];
                if (request == null || !request.Enabled) continue;

                if (!_conditionEvaluator.IsValid(request, out var reason))
                {
                    Debug.LogError($"{LogPrefix} request '{request?.Id ?? "<null>"}' is invalid and will not spawn: {reason}.");
                    continue;
                }

                requests.Add(ActiveRequestRuntime.FromCondition(
                    request,
                    _textComposer.Compose(request),
                    _genreResolver.Resolve(request)));
            }

            return requests;
        }
    }
}
