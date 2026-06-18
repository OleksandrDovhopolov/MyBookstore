using Game.Commands;
using Game.Configs;
using Game.Configs.Remote;
using Game.Configs.Server;
using Game.Http;
using VContainer;
using VContainer.Unity;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope — конфиги переживают смену сцен).
    // ServerConfigSource зависит от InfrastructureVContainerBindings
    // (IConnectionService / ICommandLogger / ICommandErrorReporter).
    public static class ConfigsVContainerBindings
    {
        public static void RegisterConfigs(this IContainerBuilder builder)
        {
            // Override-слой = Firebase RC. С NullRemoteConfigService ведёт себя как base configs
            // (нет override'ов) — потребители ConfigsService не меняются при включении RC.
#if BOOKSTORE_FIREBASE_RC
            builder.Register<IRemoteConfigService, FirebaseRemoteConfigService>(Lifetime.Singleton);
#else
            builder.Register<IRemoteConfigService, NullRemoteConfigService>(Lifetime.Singleton);
#endif
            builder.Register<IConfigOverrideSource, RemoteConfigOverrideSource>(Lifetime.Singleton);

            builder.Register<IConfigsBackendConfig, ConfigsBackendConfig>(Lifetime.Singleton);

            // Base source. Build → всегда сервер. Editor → локальная папка по умолчанию,
            // с тумблером Tools/Configs/Use Server Source для проверки интеграции.
#if UNITY_EDITOR
            if (UseServerSourceInEditor)
                builder.Register<IConfigSource>(ResolveServerSource, Lifetime.Singleton);
            else
                builder.Register<IConfigSource>(_ => new LocalFolderConfigSource(), Lifetime.Singleton);
#else
            builder.Register<IConfigSource>(ResolveServerSource, Lifetime.Singleton);
#endif

            builder.Register<IConfigsService, ConfigsService>(Lifetime.Singleton);

            // Прогрев конфигов — теперь часть LoadingOrchestrator (ConfigsWarmupOperation).
            // См. Game.Bootstrap.Loading.LoadingOrchestratorEntryPoint.
        }

        private static IConfigSource ResolveServerSource(IObjectResolver r)
            => new ServerConfigSource(
                r.Resolve<IConfigsBackendConfig>(),
                r.Resolve<IConnectionService>(),
                r.Resolve<ICommandLogger>(),
                r.Resolve<ICommandErrorReporter>(),
                CreateBundledDefaults());

        // Editor: live Assets/Configs/ для hot-reload.
        // Build: StreamingAssets/Configs/ (через UnityWebRequest — на Android прямой File.IO недоступен).
        // Регламент синхронизации: Tools/Configs/Sync Bundled Defaults to StreamingAssets.
        private static IConfigSource CreateBundledDefaults()
        {
#if UNITY_EDITOR
            return new LocalFolderConfigSource();
#else
            return new StreamingAssetsConfigSource();
#endif
        }

#if UNITY_EDITOR
        private const string UseServerPrefKey = "MyBookstore.Configs.UseServerSource";
        private const string MenuPath = "Tools/Configs/Use Server Source";
        private const string ClearSnapshotMenuPath = "Tools/Configs/Clear Server Snapshot";

        private static bool UseServerSourceInEditor => EditorPrefs.GetBool(UseServerPrefKey, false);

        [MenuItem(MenuPath)]
        private static void ToggleServerSource()
            => EditorPrefs.SetBool(UseServerPrefKey, !UseServerSourceInEditor);

        [MenuItem(MenuPath, true)]
        private static bool ToggleServerSourceValidate()
        {
            Menu.SetChecked(MenuPath, UseServerSourceInEditor);
            return true;
        }

        // Удаляет persistentDataPath/configs/ (snapshot + saved manifest с etag'ами).
        // Используй после ручной правки БД на сервере, если клиент держит старое содержимое.
        // На следующем Play ServerConfigSource зальёт всё заново.
        [MenuItem(ClearSnapshotMenuPath)]
        private static void ClearServerSnapshot()
        {
            var dir = System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "configs");
            if (System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.Delete(dir, recursive: true);
                UnityEngine.Debug.Log($"[Configs] Snapshot cleared: {dir}");
            }
            else
            {
                UnityEngine.Debug.Log($"[Configs] Nothing to clear ({dir} doesn't exist).");
            }
        }
#endif
    }
}
