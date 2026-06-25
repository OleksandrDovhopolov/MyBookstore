using System;
using System.Collections.Generic;
using Game.Conditions.API;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.Conditions.Services
{
    /// <summary>
    /// Default <see cref="IConditionParser"/>. Structural keys <c>all</c> / <c>any</c> / <c>not</c>
    /// are handled here; everything else is a leaf dispatched to a registered
    /// <see cref="IConditionFactory"/> by its <c>"type"</c>. Fail-closed: unknown/malformed nodes
    /// become <see cref="NeverMetCondition"/> with an error log.
    /// </summary>
    public sealed class ConditionParser : IConditionParser
    {
        private const string LogPrefix = "[Conditions]";

        private readonly IConditionFactoryRegistry _registry;

        public ConditionParser(IConditionFactoryRegistry registry)
            => _registry = registry ?? throw new ArgumentNullException(nameof(registry));

        public ICondition Parse(JObject node)
        {
            // No requirements → always available.
            if (node == null || !node.HasValues) return AlwaysMetCondition.Instance;

            if (node["all"] is JArray all) return new AllOfCondition(ParseMany(all));
            if (node["any"] is JArray any) return new AnyOfCondition(ParseMany(any));
            if (node["not"] is JObject not) return new NotCondition(Parse(not));

            var type = node.Value<string>("type");
            if (string.IsNullOrEmpty(type))
            {
                Debug.LogError($"{LogPrefix} condition node has no composite key and no 'type'; " +
                               $"treated as never-met. Node: {node}");
                return new NeverMetCondition("invalid");
            }

            if (!_registry.TryGet(type, out var factory))
            {
                Debug.LogError($"{LogPrefix} no factory registered for condition type '{type}'; " +
                               $"treated as never-met.");
                return new NeverMetCondition($"unknown.{type}");
            }

            try
            {
                return factory.Create(node) ?? new NeverMetCondition($"invalid.{type}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogPrefix} factory '{type}' failed to build condition: {ex.Message}; " +
                               $"treated as never-met.");
                return new NeverMetCondition($"invalid.{type}");
            }
        }

        private IReadOnlyList<ICondition> ParseMany(JArray array)
        {
            var list = new List<ICondition>(array.Count);
            foreach (var token in array)
            {
                if (token is JObject obj)
                    list.Add(Parse(obj));
                else
                    Debug.LogWarning($"{LogPrefix} skipping non-object entry in composite: {token}");
            }
            return list;
        }
    }
}
