using UnityEngine;
using VContainer;

namespace Game.Bootstrap
{
    // ScriptableObjectInstaller — lives under Script Installers on the GlobalLifetimeScope.prefab.
    // Registers application-wide singletons that survive scene transitions.
    // The starting gameplay scene name now lives as a SerializeField on the Bootstrap MonoBehaviour
    // (in the boot scene), so there is no _gameplaySceneName / LoadingSettings here.
    [CreateAssetMenu(fileName = "BootstrapInstaller", menuName = "Game/Installers/BootstrapInstaller")]
    public class BootstrapInstaller : ScriptableObjectInstaller
    {
#if UNITY_EDITOR
        [Header("Debug Start (Editor only)")]
        [Tooltip("Master switch. When off, the debug flags below are ignored.")]
        [SerializeField] private bool _useDebugFeatures = false;

        [Tooltip("Skip Addressables update and RemoteConfig init. Uses the bundled catalog + base configs.")]
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
            builder.RegisterInventory();
            builder.RegisterResources();
            builder.RegisterProgression();
            builder.RegisterFtue();
        }

        private void ApplyDebugFlags()
        {
#if UNITY_EDITOR
            Game.Bootstrap.Loading.DebugStartFlags.UseDebugFeatures = _useDebugFeatures;
            Game.Bootstrap.Loading.DebugStartFlags.SkipFullLoading = _useDebugFeatures && _skipFullLoading;
            if (Game.Bootstrap.Loading.DebugStartFlags.UseDebugFeatures)
            {
                Debug.LogWarning(
                    $"[BootstrapInstaller] Debug flags ON. SkipFullLoading={Game.Bootstrap.Loading.DebugStartFlags.SkipFullLoading}");
            }
#endif
        }
    }
}
