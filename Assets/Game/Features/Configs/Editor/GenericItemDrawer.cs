using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Configs.Editor
{
    /// <summary>
    /// Рекурсивный JObject-редактор (§4.3 generic mode):
    /// string/int/float/bool — поля по типу; вложенные object/array — foldout.
    /// Структуру (имена полей, типы) не меняем — это закроет Raw JSON mode (§10).
    /// </summary>
    internal sealed class GenericItemDrawer
    {
        private readonly HashSet<string> _expanded = new();

        public bool Draw(JObject item)
        {
            if (item == null) return false;
            return DrawObject(item, "$");
        }

        private bool DrawObject(JObject obj, string path)
        {
            var changed = false;
            foreach (var prop in obj.Properties())
            {
                var childPath = path + "." + prop.Name;
                changed |= DrawValue(prop.Name, prop.Value, childPath, v => prop.Value = v);
            }
            return changed;
        }

        private bool DrawValue(string label, JToken token, string path, System.Action<JToken> replace)
        {
            switch (token.Type)
            {
                case JTokenType.String:
                    {
                        var cur = token.Value<string>() ?? string.Empty;
                        var next = EditorGUILayout.TextField(label, cur);
                        if (next == cur) return false;
                        replace(new JValue(next));
                        return true;
                    }
                case JTokenType.Integer:
                    {
                        var cur = token.Value<long>();
                        var next = EditorGUILayout.LongField(label, cur);
                        if (next == cur) return false;
                        replace(new JValue(next));
                        return true;
                    }
                case JTokenType.Float:
                    {
                        var cur = token.Value<double>();
                        // Unity EditorGUILayout.DoubleField есть в новых версиях; FloatField проще и хватает для конфигов.
                        var next = EditorGUILayout.FloatField(label, (float)cur);
                        if (Mathf.Approximately(next, (float)cur)) return false;
                        replace(new JValue((double)next));
                        return true;
                    }
                case JTokenType.Boolean:
                    {
                        var cur = token.Value<bool>();
                        var next = EditorGUILayout.Toggle(label, cur);
                        if (next == cur) return false;
                        replace(new JValue(next));
                        return true;
                    }
                case JTokenType.Null:
                    {
                        EditorGUILayout.LabelField(label, "null (edit in Raw JSON)");
                        return false;
                    }
                case JTokenType.Object:
                    {
                        var open = _expanded.Contains(path);
                        var nowOpen = EditorGUILayout.Foldout(open, label + "  { … }", true);
                        if (nowOpen != open)
                        {
                            if (nowOpen) _expanded.Add(path); else _expanded.Remove(path);
                        }
                        if (nowOpen)
                        {
                            using (new EditorGUI.IndentLevelScope())
                                return DrawObject((JObject)token, path);
                        }
                        return false;
                    }
                case JTokenType.Array:
                    {
                        var arr = (JArray)token;
                        var changed = false;
                        var open = _expanded.Contains(path);
                        var nowOpen = EditorGUILayout.Foldout(open, $"{label}  [ {arr.Count} ]", true);
                        if (nowOpen != open)
                        {
                            if (nowOpen) _expanded.Add(path); else _expanded.Remove(path);
                        }
                        if (nowOpen)
                        {
                            using (new EditorGUI.IndentLevelScope())
                                for (var i = 0; i < arr.Count; i++)
                                {
                                    var idx = i; // capture
                                    changed |= DrawValue($"[{idx}]", arr[idx], path + "[" + idx + "]", v => arr[idx] = v);
                                }
                        }
                        return changed;
                    }
                default:
                    EditorGUILayout.LabelField(label, token.Type.ToString());
                    return false;
            }
        }
    }
}
