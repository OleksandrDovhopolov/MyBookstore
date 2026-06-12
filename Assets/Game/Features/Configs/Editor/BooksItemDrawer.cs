using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Configs.Editor
{
    /// <summary>
    /// Типизированный редактор книги (§4.3 спеки): id/title/author/genre/basePrice/rarityWeight.
    /// Пишет обратно в JObject — мутации видны всему окну через SectionState.WorkingArray.
    /// </summary>
    internal static class BooksItemDrawer
    {
        public static void Draw(JObject item)
        {
            if (item == null) return;

            EditorGUILayout.LabelField("Book", EditorStyles.boldLabel);

            DrawString(item, "id", "Id");
            DrawString(item, "title", "Title");
            DrawString(item, "author", "Author");
            DrawString(item, "genre", "Genre");
            DrawInt(item, "basePrice", "Base Price");
            DrawFloat(item, "rarityWeight", "Rarity Weight");
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
                ? obj[field].Value<float>() : 0f;
            var next = EditorGUILayout.FloatField(label, current);
            if (!Mathf.Approximately(next, current)) obj[field] = next;
        }
    }
}
