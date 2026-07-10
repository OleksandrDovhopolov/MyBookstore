using System;

namespace UIShared
{
    public interface IResourceCounterTargetRegistry
    {
        event Action<IResourceCounterTarget> TargetRegistered;

        void Register(IResourceCounterTarget target);
        void Unregister(IResourceCounterTarget target);
        bool TryGetTarget(string resourceId, out IResourceCounterTarget target);
    }
}
