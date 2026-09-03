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
        public static void Draw(JObject item)
        {
            if (item == null) return;

            EditorGUILayout.LabelField("Book", EditorStyles.boldLabel);

            DrawString(item, "id", "Id");
            DrawString(item, "titleKey", "Title Key");
            DrawString(item, "authorKey", "Author Key");
            DrawString(item, "descriptionKey", "Description Key");
            DrawStringArray(item, "genres", "Genres");
            DrawFloat(item, "rarityWeight", "Rarity Weight");
            DrawInt(item, "published", "Published");
            DrawInt(item, "pages", "Pages");
            DrawString(item, "fakeOrReal", "Fake or Real");
            EditorGUILayout.Space(8);
            DrawStringArray(item, "qualities", "Qualities");
        }

        private static void DrawString(JObject obj, string field, string label)
        {
            var current = obj[field]?.Type == JTokenType.String ? obj[field].Value<string>() : string.Empty;
            var next = EditorGUILayout.TextField(label, current);
            if (next != current) obj[field] = next;
        }

        private static void DrawInt(JObject obj, string field, string label)
        {
            var current = obj[field]?.Type is JTokenType.Integer or JTokenType.Float
                ? obj[field].Value<int>() : 0;
            var next = EditorGUILayout.IntField(label, current);
            if (next != current) obj[field] = next;
        }

        private static void DrawFloat(JObject obj, string field, string label)
        {
            var current = obj[field]?.Type is JTokenType.Float or JTokenType.Integer
                ? obj[field].Value<float>() : 0.5f;
            var next = EditorGUILayout.FloatField(label, current);
            if (!Mathf.Approximately(next, current)) obj[field] = next;
        }

        private static void DrawStringArray(JObject obj, string field, string label)
        {
            var arr = obj[field] as JArray;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            if (GUILayout.Button("+", GUILayout.Width(28)))
            {
                arr ??= new JArray();
                obj[field] = arr;
                arr.Add(string.Empty);
            }
            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.IndentLevelScope())
            {
                if (arr == null || arr.Count == 0)
                {
                    EditorGUILayout.LabelField("Empty");
                    return;
                }

                for (var i = 0; i < arr.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    var current = arr[i]?.Type == JTokenType.String ? arr[i].Value<string>() : string.Empty;
                    var next = EditorGUILayout.TextField($"[{i}]", current);
                    if (next != current)
                        arr[i] = next;

                    if (GUILayout.Button("-", GUILayout.Width(28)))
                    {
                        arr.RemoveAt(i);
                        EditorGUILayout.EndHorizontal();
                        GUI.FocusControl(null);
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
        }
    }
}
