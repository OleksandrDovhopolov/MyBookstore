using UnityEngine;
using VContainer;

namespace Game.Bootstrap
{
    public abstract class MonoInstaller : MonoBehaviour
    {
        public abstract void InstallBindings(IContainerBuilder builder);
    }
}
