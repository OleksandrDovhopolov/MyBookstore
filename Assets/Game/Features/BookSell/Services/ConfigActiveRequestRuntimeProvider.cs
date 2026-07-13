using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using Game.Configs;
using Game.Configs.Models;
using UnityEngine;

namespace Book.Sell.Services
{
    public sealed class ConfigActiveRequestRuntimeProvider : IActiveRequestRuntimeProvider
    {
        private const string LogPrefix = "[ActiveRequests]";

        private readonly IConfigsService _configs;
        private readonly IBookConditionRequestEvaluator _conditionEvaluator;

        public ConfigActiveRequestRuntimeProvider(
            IConfigsService configs,
            IBookConditionRequestEvaluator conditionEvaluator)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _conditionEvaluator = conditionEvaluator ?? throw new ArgumentNullException(nameof(conditionEvaluator));
        }

        public IReadOnlyList<ActiveRequestRuntime> GetRequests(ActiveRequestMode mode)
        {
            switch (mode)
            {
                case ActiveRequestMode.LegacyScoring:
                    return LegacyRequests();
                case ActiveRequestMode.Conditions:
                    return ConditionRequests();
                default:
                    return Array.Empty<ActiveRequestRuntime>();
            }
        }

        private IReadOnlyList<ActiveRequestRuntime> LegacyRequests()
        {
            var configs = _configs.GetAll<RequestConfig>();
            var requests = new List<ActiveRequestRuntime>(configs.Count);
            for (var i = 0; i < configs.Count; i++)
            {
                var runtime = ActiveRequestRuntime.FromLegacy(configs[i]);
                if (runtime != null) requests.Add(runtime);
            }

            return requests;
        }

        private IReadOnlyList<ActiveRequestRuntime> ConditionRequests()
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
                    _conditionEvaluator.BuildDebugText(request)));
            }

            return requests;
        }
    }
}
