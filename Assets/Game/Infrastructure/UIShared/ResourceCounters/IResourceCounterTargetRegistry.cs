using System;
using System.Collections.Generic;

namespace UIShared
{
    public interface IResourceCounterTargetRegistry
    {
        event Action<IResourceCounterTarget> TargetRegistered;

        void Register(IResourceCounterTarget target);
        void Unregister(IResourceCounterTarget target);

        /// <summary>The active target — the most recently registered live one. Count-up animates here.</summary>
        bool TryGetTarget(string resourceId, out IResourceCounterTarget target);

        /// <summary>Every live target for the resource. Amount updates are pushed to all of them.</summary>
        IReadOnlyList<IResourceCounterTarget> GetTargets(string resourceId);
    }
}
