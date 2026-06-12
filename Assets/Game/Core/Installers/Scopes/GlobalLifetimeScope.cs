using UnityEngine;
using VContainer;

namespace Game.Bootstrap
{
    public class GlobalLifetimeScope : BaseLifetimeScope
    {
        protected override void Awake()
        {
            // ВАЖНО: сначала build контейнера (base.Awake), потом DontDestroyOnLoad.
            // Если поменять порядок — FindComponentProvider ищет LoadingScreenView
            // в DDOL-сцене вместо boot-сцены и падает с "is not in this scene DontDestroyOnLoad".
            base.Awake();
            DontDestroyOnLoad(gameObject);
        }

        protected override void InstallBindings(IContainerBuilder builder)
        {
        }
    }
}
