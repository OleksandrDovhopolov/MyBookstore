using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Bootstrap.EditorTools
{
    /// <summary>
    /// Заставляет Play-режим ВСЕГДА стартовать со сцены Bootstrap, какая бы сцена ни была открыта.
    /// Вся загрузка идёт только через Bootstrap (warmup конфигов, RemoteConfig, Save, FTUE-засев, переход
    /// в GameplayScene). Прямой Play по GameplayScene/LocationScene пропускал бы этот пайплайн (пустой
    /// инвентарь, непрогретые конфиги и т.п.) — теперь это невозможно. См. docs/GameFlowLoop.md.
    ///
    /// Тумблер: Tools/Game/Play From Bootstrap (галочка). Выключи, если нужно временно играть с открытой сцены.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeStartSceneSetup
    {
        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string MenuPath = "Tools/Game/Play From Bootstrap";
        private const string EnabledKey = "Game.PlayFromBootstrap.Enabled";

        static PlayModeStartSceneSetup()
        {
            // Откладываем до готовности AssetDatabase (static ctor может выполниться во время импорта).
            EditorApplication.delayCall += Apply;
        }

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, true);
            set => EditorPrefs.SetBool(EnabledKey, value);
        }

        private static void Apply()
        {
            if (!Enabled)
            {
                EditorSceneManager.playModeStartScene = null;
                return;
            }

            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath);
            if (scene == null)
            {
                Debug.LogWarning($"[PlayFromBootstrap] Bootstrap scene not found at '{BootstrapScenePath}'. " +
                                 "Play mode start scene not overridden.");
                EditorSceneManager.playModeStartScene = null;
                return;
            }

            EditorSceneManager.playModeStartScene = scene;
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
            Debug.Log($"[PlayFromBootstrap] {(Enabled ? "ON — Play стартует с Bootstrap" : "OFF — Play стартует с открытой сцены")}.");
        }

        [MenuItem(MenuPath, isValidateFunction: true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }
    }
}
