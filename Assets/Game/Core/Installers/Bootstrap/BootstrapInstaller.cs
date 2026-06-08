using UnityEngine;
using VContainer;

namespace Game.Bootstrap
{
    // ScriptableObjectInstaller — assign this asset to GlobalLifetimeScope._scriptableObjectInstallers.
    // Registers application-wide singleton services that must survive scene transitions.
    [CreateAssetMenu(fileName = "BootstrapInstaller", menuName = "Game/Installers/BootstrapInstaller")]
    public class BootstrapInstaller : ScriptableObjectInstaller
    {
        public override void InstallBindings(IContainerBuilder builder)
        {
            builder.RegisterGameLoading();
            builder.RegisterAnalytics();
            builder.RegisterSave();
            builder.RegisterInfrastructure();
            builder.RegisterUiSystem();
        }
    }
}
