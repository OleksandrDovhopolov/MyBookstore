using Game.Bootstrap.Loading;
using UnityEngine;
using VContainer;

namespace Game.Bootstrap
{
    // ScriptableObjectInstaller — assign this asset to GlobalLifetimeScope._scriptableObjectInstallers.
    // Registers application-wide singleton services that must survive scene transitions.
    [CreateAssetMenu(fileName = "BootstrapInstaller", menuName = "Game/Installers/BootstrapInstaller")]
    public class BootstrapInstaller : ScriptableObjectInstaller
    {
#if UNITY_EDITOR
        [Header("Debug Start (Editor only)")]
        [Tooltip("Master switch. Если выключен — debug-флаги ниже игнорируются.")]
        [SerializeField] private bool _useDebugFeatures = false;

        [Tooltip("Пропустить Addressables update и RemoteConfig init. Используются bundled catalog + base configs.")]
        [SerializeField] private bool _skipFullLoading = false;
#endif

        public override void InstallBindings(IContainerBuilder builder)
        {
            ApplyDebugFlags();

            builder.RegisterGameLoading();
            builder.RegisterAnalytics();
            builder.RegisterSave();
            builder.RegisterInfrastructure();
            builder.RegisterConfigs();
            builder.RegisterUiSystem();
        }

        private void ApplyDebugFlags()
        {
#if UNITY_EDITOR
            DebugStartFlags.UseDebugFeatures = _useDebugFeatures;
            DebugStartFlags.SkipFullLoading = _useDebugFeatures && _skipFullLoading;
            if (DebugStartFlags.UseDebugFeatures)
            {
                Debug.LogWarning(
                    $"[BootstrapInstaller] Debug flags ON. SkipFullLoading={DebugStartFlags.SkipFullLoading}");
            }
#endif
        }
    }
}
