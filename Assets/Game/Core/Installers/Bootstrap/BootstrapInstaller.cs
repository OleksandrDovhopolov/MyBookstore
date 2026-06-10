using Game.Bootstrap.Loading;
using UnityEngine;
using VContainer;
// LoadingSettings — DTO в Loading-сборке (см. LoadingSettings.cs).

namespace Game.Bootstrap
{
    // ScriptableObjectInstaller — assign this asset to GlobalLifetimeScope._scriptableObjectInstallers.
    // Registers application-wide singleton services that must survive scene transitions.
    [CreateAssetMenu(fileName = "BootstrapInstaller", menuName = "Game/Installers/BootstrapInstaller")]
    public class BootstrapInstaller : ScriptableObjectInstaller
    {
        [Header("Scene Transition")]
        [Tooltip("Имя сцены, в которую переходит лоадер после успешного завершения всех операций. " +
                 "Сцена должна быть добавлена в Build Settings.")]
        [SerializeField] private string _gameplaySceneName = "GameplayScene";

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

            // Настройки лоадера передаём через DTO в Loading-сборке.
            // Сам SO в DI не регистрируем — это бы потребовало ссылку Loading→Bootstrap (циклика).
            builder.RegisterInstance(new LoadingSettings(_gameplaySceneName));

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
