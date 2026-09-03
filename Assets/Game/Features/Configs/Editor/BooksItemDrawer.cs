using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Configs.Editor
{
    /// <summary>
    /// Типизированный редактор книги (§4.3 спеки): id/titleKey/authorKey/descriptionKey/genres/qualities/rarityWeight.
    /// Пишет обратно в JObject — мутации видны всему окну через SectionState.WorkingArray.
    /// </summary>
    internal static class BooksItemDrawer
    {
        public static bool Draw(JObject item)
        {
            if (item == null) return false;
            var changed = false;

            EditorGUILayout.LabelField("Book", EditorStyles.boldLabel);

            changed |= DrawString(item, "id", "Id");
            changed |= DrawString(item, "titleKey", "Title Key");
            changed |= DrawString(item, "authorKey", "Author Key");
            changed |= DrawString(item, "descriptionKey", "Description Key");
            changed |= DrawStringArray(item, "genres", "Genres");
            changed |= DrawFloat(item, "rarityWeight", "Rarity Weight");
            changed |= DrawInt(item, "published", "Published");
            changed |= DrawInt(item, "pages", "Pages");
            changed |= DrawString(item, "fakeOrReal", "Fake or Real");
            EditorGUILayout.Space(8);
            changed |= DrawStringArray(item, "qualities", "Qualities");
            return changed;
        }

        private static bool DrawString(JObject obj, string field, string label)
        {
            var current = obj[field]?.Type == JTokenType.String ? obj[field].Value<string>() : string.Empty;
            var next = EditorGUILayout.TextField(label, current);
            if (next == current) return false;
            obj[field] = next;
            return true;
        }

        private static bool DrawInt(JObject obj, string field, string label)
        {
            var current = obj[field]?.Type is JTokenType.Integer or JTokenType.Float
                ? obj[field].Value<int>() : 0;
            var next = EditorGUILayout.IntField(label, current);
            if (next == current) return false;
            obj[field] = next;
            return true;
        }

        private static bool DrawFloat(JObject obj, string field, string label)
        {
            var current = obj[field]?.Type is JTokenType.Float or JTokenType.Integer
                ? obj[field].Value<float>() : 0.5f;
            var next = EditorGUILayout.FloatField(label, current);
            if (Mathf.Approximately(next, current)) return false;
            obj[field] = next;
            return true;
        }

        private static bool DrawStringArray(JObject obj, string field, string label)
        {
            var arr = obj[field] as JArray;
            var changed = false;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            if (GUILayout.Button("+", GUILayout.Width(28)))
            {
                arr ??= new JArray();
                obj[field] = arr;
                arr.Add(string.Empty);
                changed = true;
            }
            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.IndentLevelScope())
            {
                if (arr == null || arr.Count == 0)
                {
                    EditorGUILayout.LabelField("Empty");
                    return changed;
                }

                for (var i = 0; i < arr.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    var current = arr[i]?.Type == JTokenType.String ? arr[i].Value<string>() : string.Empty;
                    var next = EditorGUILayout.TextField($"[{i}]", current);
                    if (next != current)
                    {
                        arr[i] = next;
                        changed = true;
                    }

                    if (GUILayout.Button("-", GUILayout.Width(28)))
                    {
                        arr.RemoveAt(i);
                        changed = true;
                        EditorGUILayout.EndHorizontal();
                        GUI.FocusControl(null);
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            return changed;
        }
    }
}
