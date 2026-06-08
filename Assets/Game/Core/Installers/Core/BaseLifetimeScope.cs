using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    public abstract class BaseLifetimeScope : LifetimeScope
    {
        [SerializeField] private List<MonoInstaller> _monoInstallers;
        [SerializeField] private List<ScriptableObjectInstaller> _scriptableObjectInstallers;

        protected sealed override void Configure(IContainerBuilder builder)
        {
            InstallBindings(builder);
            InstallScriptableObjects(builder);
            InstallMonoBehaviours(builder);

            builder.RegisterBuildCallback(OnBuildCallback);
        }

        protected abstract void InstallBindings(IContainerBuilder builder);

        protected virtual void OnBuildCallback(IObjectResolver resolver)
        {
        }

        private void InstallScriptableObjects(IContainerBuilder builder)
        {
            foreach (var installer in _scriptableObjectInstallers)
            {
                installer.InstallBindings(builder);
            }
        }

        private void InstallMonoBehaviours(IContainerBuilder builder)
        {
            foreach (var installer in _monoInstallers)
            {
                installer.InstallBindings(builder);
            }
        }
    }
}
