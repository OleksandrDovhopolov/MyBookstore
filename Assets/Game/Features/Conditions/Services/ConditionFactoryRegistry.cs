using System;
using System.Collections.Generic;
using Game.Conditions.API;
using UnityEngine;

namespace Game.Conditions.Services
{
    /// <summary>
    /// Indexes all DI-registered <see cref="IConditionFactory"/> by their type discriminator
    /// (case-insensitive). Mirrors how <c>InventoryUseRouter</c> receives its handlers — VContainer
    /// injects the full <see cref="IReadOnlyList{T}"/> of registered factories.
    /// </summary>
    public sealed class ConditionFactoryRegistry : IConditionFactoryRegistry
    {
        private const string LogPrefix = "[Conditions]";

        private readonly Dictionary<string, IConditionFactory> _byType =
            new(StringComparer.OrdinalIgnoreCase);

        public ConditionFactoryRegistry(IReadOnlyList<IConditionFactory> factories)
        {
            if (factories == null) return;
            for (var i = 0; i < factories.Count; i++)
            {
                var factory = factories[i];
                if (factory == null || string.IsNullOrEmpty(factory.Type)) continue;

                if (_byType.ContainsKey(factory.Type))
                {
                    Debug.LogError($"{LogPrefix} duplicate condition factory for type '{factory.Type}'; " +
                                   $"keeping the first, ignoring {factory.GetType().Name}.");
                    continue;
                }
                _byType[factory.Type] = factory;
            }
        }

        public bool TryGet(string type, out IConditionFactory factory)
        {
            if (!string.IsNullOrEmpty(type)) return _byType.TryGetValue(type, out factory);
            factory = null;
            return false;
        }
    }
}
