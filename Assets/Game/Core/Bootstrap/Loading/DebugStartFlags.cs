using UnityEngine;

namespace Game.Bootstrap.Loading
{
    // Editor-only debug-флаги быстрого старта. В Build все флаги принудительно false —
    // ветки в LoadingOrchestratorEntryPoint компилируются под #if UNITY_EDITOR.
    //
    // Сброс на старте Play через RuntimeInitializeOnLoadMethod, потому что static-поля
    // переживают перезапуск Play Mode в редакторе.
    //
    // Значения проставляет BootstrapInstaller.InstallBindings() из своих SerializeField.
    public static class DebugStartFlags
    {
        public static bool UseDebugFeatures { get; set; }

        // Пропустить тяжёлые фазы (Addressables update + RemoteConfig init).
        // Используются bundled catalog + base configs. Полезно при итерации UI/логики
        // без сетевых ожиданий.
        public static bool SkipFullLoading { get; set; }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlayStart()
        {
            UseDebugFeatures = false;
            SkipFullLoading = false;
        }
#endif
    }
}
