using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.ContentWidget
{
    /*
     * both classes which use this one can use DI. WidgetRegistry could be made registered via binder
     */
    public static class WidgetRegistry
    {
        private static readonly Dictionary<Type, MonoBehaviour> Prefabs = new();

        public static void Register<TData>(MonoBehaviour prefab)
            where TData : ContentWidgetDataBase
        {
            if (prefab == null)
            {
                Prefabs.Remove(typeof(TData));
                return;
            }

            if (prefab is not IContentWidgetView)
            {
                Debug.LogError($"[WidgetRegistry] Prefab '{prefab.name}' must implement {nameof(IContentWidgetView)}.");
                return;
            }

            Prefabs[typeof(TData)] = prefab;
        }

        public static void Unregister<TData>(MonoBehaviour prefab = null)
            where TData : ContentWidgetDataBase
        {
            var key = typeof(TData);
            if (prefab == null)
            {
                Prefabs.Remove(key);
                return;
            }

            if (Prefabs.TryGetValue(key, out var current) && ReferenceEquals(current, prefab))
                Prefabs.Remove(key);
        }

        public static MonoBehaviour GetPrefab(Type dataType)
        {
            if (dataType == null) return null;
            Prefabs.TryGetValue(dataType, out var prefab);
            return prefab;
        }

        public static void Clear() => Prefabs.Clear();
    }
}
