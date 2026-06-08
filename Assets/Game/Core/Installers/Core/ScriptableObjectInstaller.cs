using UnityEngine;
using VContainer;

namespace Game.Bootstrap
{
    public abstract class ScriptableObjectInstaller : ScriptableObject
    {
        public abstract void InstallBindings(IContainerBuilder builder);
    }
}
