using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Tutorial.Steps
{
    /// <summary>
    /// Indexes all DI-registered <see cref="ITutorialStepHandler"/> by their type discriminator
    /// (case-insensitive). Mirrors ConditionFactoryRegistry — VContainer injects the full
    /// <see cref="IReadOnlyList{T}"/> of registered handlers.
    /// </summary>
    public sealed class TutorialStepHandlerRegistry
    {
        private const string LogPrefix = "[Tutorial]";

        private readonly Dictionary<string, ITutorialStepHandler> _byType =
            new(StringComparer.OrdinalIgnoreCase);

        public TutorialStepHandlerRegistry(IReadOnlyList<ITutorialStepHandler> handlers)
        {
            if (handlers == null) return;
            for (var i = 0; i < handlers.Count; i++)
            {
                var handler = handlers[i];
                if (handler == null || string.IsNullOrEmpty(handler.Type)) continue;

                if (_byType.ContainsKey(handler.Type))
                {
                    Debug.LogError($"{LogPrefix} duplicate step handler for type '{handler.Type}'; " +
                                   $"keeping the first, ignoring {handler.GetType().Name}.");
                    continue;
                }
                _byType[handler.Type] = handler;
            }
        }

        public bool TryGet(string type, out ITutorialStepHandler handler)
        {
            if (!string.IsNullOrEmpty(type)) return _byType.TryGetValue(type, out handler);
            handler = null;
            return false;
        }
    }
}
