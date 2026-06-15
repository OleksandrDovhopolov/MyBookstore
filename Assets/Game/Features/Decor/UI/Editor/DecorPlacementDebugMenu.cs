using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Decor.UI.Editor
{
    /// <summary>
    /// Editor-only diagnostics for <see cref="DecorPlacementScreenView"/>. Reproduces the body of
    /// <c>SpawnSlotRow</c>/<c>SpawnInventoryRow</c> in edit mode so we can check why a row doesn't appear
    /// without going through DI, configs or the placement service.
    /// </summary>
    public static class DecorPlacementDebugMenu
    {
        private const string MenuRoot = "Tools/Decor/";

        [MenuItem(MenuRoot + "Inspect Placement View", priority = 0)]
        private static void Inspect()
        {
            if (!TryGetView(out var view)) return;
            LogField(view, "_slotRowTemplate");
            LogField(view, "_slotListRoot");
            LogField(view, "_inventoryRowTemplate");
            LogField(view, "_inventoryListRoot");
            LogField(view, "_summaryLabel");
            LogField(view, "_capHintLabel");
            LogField(view, "_clearAllButton");
            LogField(view, "_closeButton");
            EditorGUIUtility.PingObject(view);
            Selection.activeGameObject = view.gameObject;
        }

        [MenuItem(MenuRoot + "Test Spawn Slot Row", priority = 10)]
        private static void TestSpawnSlotRow()
        {
            if (!TryGetView(out var view)) return;
            TrySpawn(view, "_slotRowTemplate", "_slotListRoot", "slot");
        }

        [MenuItem(MenuRoot + "Test Spawn Inventory Row", priority = 11)]
        private static void TestSpawnInventoryRow()
        {
            if (!TryGetView(out var view)) return;
            TrySpawn(view, "_inventoryRowTemplate", "_inventoryListRoot", "inventory");
        }

        [MenuItem(MenuRoot + "Clear Test Clones", priority = 20)]
        private static void ClearTestClones()
        {
            if (!TryGetView(out var view)) return;
            var removed = 0;
            foreach (var t in view.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || t == view.transform) continue;
                if (!t.name.Contains("(test clone)")) continue;
                Undo.DestroyObjectImmediate(t.gameObject);
                removed++;
            }
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            Debug.Log($"[Decor Tools] Удалено тест-клонов: {removed}.");
        }

        private static void TrySpawn(DecorPlacementScreenView view, string templateFieldName, string rootFieldName, string kind)
        {
            var template = GetField(view, templateFieldName) as Component;
            var root = GetField(view, rootFieldName) as Transform;

            Debug.Log($"[Decor Tools] === Test Spawn ({kind}) on '{view.name}' ===");
            Debug.Log($"[Decor Tools] {templateFieldName} = {Describe(template)}");
            Debug.Log($"[Decor Tools] {rootFieldName} = {Describe(root)}");

            if (template == null)
            {
                Debug.LogError($"[Decor Tools] {templateFieldName} не назначен в инспекторе — `SpawnSlotRow`/`SpawnInventoryRow` молча вернёт null и список будет пустой.");
                return;
            }
            if (root == null)
            {
                Debug.LogError($"[Decor Tools] {rootFieldName} не назначен — некуда инстанцировать.");
                return;
            }

            if (!template.gameObject.scene.IsValid() && !PrefabUtility.IsPartOfAnyPrefab(template))
                Debug.LogWarning("[Decor Tools] Шаблон не лежит ни в сцене, ни в префабе — это странно, но Instantiate всё равно попробуем.");

            if (root.gameObject.activeInHierarchy == false)
                Debug.LogWarning($"[Decor Tools] Root '{root.name}' выключен в иерархии — клон создастся, но виден не будет.");

            var before = root.childCount;
            var clone = Object.Instantiate(template, root);
            clone.name = template.name + " (test clone)";
            clone.gameObject.SetActive(true);
            Undo.RegisterCreatedObjectUndo(clone.gameObject, "Decor Tools: Test Spawn");
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            EditorGUIUtility.PingObject(clone);
            Selection.activeGameObject = clone.gameObject;

            Debug.Log($"[Decor Tools] OK: создан клон '{clone.name}' под '{root.name}'. children: {before} → {root.childCount}.");
        }

        private static bool TryGetView(out DecorPlacementScreenView view)
        {
            view = null;
            var all = Resources.FindObjectsOfTypeAll<DecorPlacementScreenView>();
            foreach (var v in all)
            {
                if (v == null) continue;
                if (!v.gameObject.scene.IsValid()) continue;
                view = v;
                break;
            }
            if (view == null)
            {
                Debug.LogError("[Decor Tools] DecorPlacementScreenView не найден в открытых сценах. Открой сцену с этим вьюшкой и повтори.");
                return false;
            }
            return true;
        }

        private static void LogField(Object owner, string fieldName)
        {
            var value = GetField(owner, fieldName);
            Debug.Log($"[Decor Tools] {fieldName} = {Describe(value)}");
        }

        private static object GetField(Object owner, string fieldName)
        {
            var field = typeof(DecorPlacementScreenView).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(owner);
        }

        private static string Describe(object value)
        {
            if (value == null || (value is Object u && u == null)) return "<NULL>";
            if (value is Component c)
                return $"{c.name} ({c.GetType().Name}, active={c.gameObject.activeSelf}, inHierarchy={c.gameObject.activeInHierarchy}, scene='{c.gameObject.scene.name}')";
            if (value is GameObject go)
                return $"{go.name} (GameObject, active={go.activeSelf}, inHierarchy={go.activeInHierarchy})";
            return value.ToString();
        }
    }
}
