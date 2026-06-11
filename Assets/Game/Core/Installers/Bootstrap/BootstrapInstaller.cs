using UnityEngine;
using VContainer;

namespace Game.Bootstrap
{
    // ScriptableObjectInstaller — лежит в Script Installers GlobalLifetimeScope.prefab.
    // Регистрирует application-wide singletons, которые переживают смену сцен.
    // Имя стартовой gameplay-сцены теперь живёт SerializeField'ом на Bootstrap MonoBehaviour
    // (в boot-сцене) — поэтому здесь нет ни _gameplaySceneName, ни LoadingSettings.
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
