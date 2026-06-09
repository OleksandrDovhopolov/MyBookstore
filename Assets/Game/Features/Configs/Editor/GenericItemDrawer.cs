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

        public void Draw(JObject item)
        {
            if (item == null) return;
            DrawObject(item, "$");
        }

        private void DrawObject(JObject obj, string path)
        {
            foreach (var prop in obj.Properties())
            {
                var childPath = path + "." + prop.Name;
                DrawValue(prop.Name, prop.Value, childPath, v => prop.Value = v);
            }
        }

        private void DrawValue(string label, JToken token, string path, System.Action<JToken> replace)
        {
            switch (token.Type)
            {
                case JTokenType.String:
                    {
                        var cur = token.Value<string>() ?? string.Empty;
                        var next = EditorGUILayout.TextField(label, cur);
                        if (next != cur) replace(new JValue(next));
                        break;
                    }
                case JTokenType.Integer:
                    {
                        var cur = token.Value<long>();
                        var next = EditorGUILayout.LongField(label, cur);
                        if (next != cur) replace(new JValue(next));
                        break;
                    }
                case JTokenType.Float:
                    {
                        var cur = token.Value<double>();
                        // Unity EditorGUILayout.DoubleField есть в новых версиях; FloatField проще и хватает для конфигов.
                        var next = EditorGUILayout.FloatField(label, (float)cur);
                        if (!Mathf.Approximately(next, (float)cur)) replace(new JValue((double)next));
                        break;
                    }
                case JTokenType.Boolean:
                    {
                        var cur = token.Value<bool>();
                        var next = EditorGUILayout.Toggle(label, cur);
                        if (next != cur) replace(new JValue(next));
                        break;
                    }
                case JTokenType.Null:
                    {
                        EditorGUILayout.LabelField(label, "null (edit in Raw JSON)");
                        break;
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
                                DrawObject((JObject)token, path);
                        }
                        break;
                    }
                case JTokenType.Array:
                    {
                        var arr = (JArray)token;
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
                                    DrawValue($"[{idx}]", arr[idx], path + "[" + idx + "]", v => arr[idx] = v);
                                }
                        }
                        break;
                    }
                default:
                    EditorGUILayout.LabelField(label, token.Type.ToString());
                    break;
            }
        }
    }
}
