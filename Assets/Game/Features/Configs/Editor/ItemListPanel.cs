using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Configs.Editor
{
    /// <summary>
    /// Левая панель: список items секции (§4.2). Поиск по id/secondary,
    /// Add / Duplicate / Delete. Selection хранится в SectionState по индексу —
    /// устойчиво к правке id в форме.
    /// </summary>
    internal sealed class ItemListPanel
    {
        private string _search = string.Empty;
        private Vector2 _scroll;

        public void Draw(SectionState state, HashSet<string> invalidIds)
        {
            EditorGUILayout.LabelField("Items", EditorStyles.boldLabel);
            DrawSearch();
            DrawButtons(state);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));

            for (var i = 0; i < state.WorkingArray.Count; i++)
            {
                if (state.WorkingArray[i] is not JObject item)
                    continue;

                var id = item["id"]?.Value<string>() ?? string.Empty;
                if (!Matches(id, item)) continue;

                var isSelected = i == state.SelectedItemIndex;
                var invalid = !string.IsNullOrEmpty(id) && invalidIds != null && invalidIds.Contains(id);
                var rowLabel = BuildRowLabel(id, item, invalid);

                var style = isSelected ? Selected : Normal;
                if (GUILayout.Button(rowLabel, style, GUILayout.ExpandWidth(true)))
                {
                    state.SelectedItemIndex = i;
                    GUI.FocusControl(null);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSearch()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search", GUILayout.Width(50));
            _search = EditorGUILayout.TextField(_search ?? string.Empty);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawButtons(SectionState state)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add"))
                AddItem(state);

            using (new EditorGUI.DisabledScope(state.SelectedItem == null))
            {
                if (GUILayout.Button("Duplicate"))
                    DuplicateItem(state);
                if (GUILayout.Button("Delete"))
                    DeleteItem(state);
            }
            EditorGUILayout.EndHorizontal();
        }

        private bool Matches(string id, JObject item)
        {
            if (string.IsNullOrEmpty(_search)) return true;
            if (!string.IsNullOrEmpty(id) && id.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            var second = SecondaryField(item);
            return !string.IsNullOrEmpty(second) && second.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildRowLabel(string id, JObject item, bool invalid)
        {
            var second = SecondaryField(item);
            var idText = string.IsNullOrEmpty(id) ? "<empty id>" : id;
            var suffix = string.IsNullOrEmpty(second) ? string.Empty : $"  ·  {second}";
            return (invalid ? "⚠ " : string.Empty) + idText + suffix;
        }

        private static string SecondaryField(JObject item)
        {
            foreach (var name in new[] { "title", "displayName", "name", "type" })
            {
                var t = item[name];
                if (t?.Type == JTokenType.String)
                    return t.Value<string>();
            }
            return null;
        }

        private static void AddItem(SectionState state)
        {
            var item = ItemTemplates.Create(state.Section);
            state.WorkingArray.Add(item);
            state.SelectedItemIndex = state.WorkingArray.Count - 1;
        }

        private static void DuplicateItem(SectionState state)
        {
            var src = state.SelectedItem;
            if (src == null) return;
            var copy = (JObject)src.DeepClone();
            copy["id"] = string.Empty; // §8.5 recommended: ГД явно задаёт новый id
            state.WorkingArray.Add(copy);
            state.SelectedItemIndex = state.WorkingArray.Count - 1;
        }

        private static void DeleteItem(SectionState state)
        {
            var target = state.SelectedItem;
            if (target == null) return;
            var id = target["id"]?.Value<string>() ?? "<empty id>";
            if (!EditorUtility.DisplayDialog("Delete item",
                $"Delete item id='{id}' from working snapshot? Server is not affected until Publish.",
                "Delete", "Cancel"))
                return;

            var idx = state.SelectedItemIndex;
            state.WorkingArray.RemoveAt(idx);
            state.SelectedItemIndex = idx < state.WorkingArray.Count
                ? idx
                : (state.WorkingArray.Count > 0 ? state.WorkingArray.Count - 1 : -1);
        }

        private static GUIStyle _normal;
        private static GUIStyle _selected;
        private static GUIStyle Normal => _normal ??= new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(6, 6, 2, 2)
        };
        private static GUIStyle Selected
        {
            get
            {
                if (_selected != null) return _selected;
                _selected = new GUIStyle(Normal) { fontStyle = FontStyle.Bold };
                _selected.normal.textColor = new Color(0.4f, 0.7f, 1f);
                return _selected;
            }
        }
    }
}
