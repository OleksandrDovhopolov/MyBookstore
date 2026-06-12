using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Configs.Editor
{
    /// <summary>
    /// Текстовый редактор раздела (§10 спеки): Format / Apply JSON.
    /// Невалидный JSON блокирует Apply.
    /// </summary>
    internal sealed class RawJsonDrawer
    {
        private string _buffer = string.Empty;
        private string _lastError;
        private string _lastSyncedFrom; // working snapshot, на основе которого собран buffer
        private Vector2 _scroll;

        public void Sync(SectionState state)
        {
            // Пересинхронизировать буфер, если working изменился (например, после Pull).
            var current = state.SerializeWorking(Newtonsoft.Json.Formatting.Indented);
            if (_lastSyncedFrom == null || _lastSyncedFrom != current)
            {
                _buffer = current;
                _lastSyncedFrom = current;
                _lastError = null;
            }
        }

        public void Draw(SectionState state)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Format", GUILayout.Width(80)))
                TryFormat();
            using (new EditorGUI.DisabledScope(_lastError != null && !TryParse(_buffer, out _)))
            {
                if (GUILayout.Button("Apply JSON", GUILayout.Width(120)))
                    TryApply(state);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            var style = new GUIStyle(EditorStyles.textArea) { wordWrap = false };
            var newBuf = EditorGUILayout.TextArea(_buffer, style, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            if (!ReferenceEquals(newBuf, _buffer))
            {
                _buffer = newBuf;
                _lastError = TryParse(_buffer, out _) ? null : "JSON parse error.";
            }

            if (!string.IsNullOrEmpty(_lastError))
                EditorGUILayout.HelpBox(_lastError, MessageType.Warning);
        }

        private void TryFormat()
        {
            if (TryParse(_buffer, out var arr))
            {
                _buffer = arr.ToString(Newtonsoft.Json.Formatting.Indented);
                _lastError = null;
            }
            else
            {
                _lastError = "Cannot format: JSON parse error.";
            }
        }

        private void TryApply(SectionState state)
        {
            if (!TryParse(_buffer, out var arr))
            {
                _lastError = "Cannot apply: JSON parse error.";
                return;
            }
            state.WorkingArray = arr;
            // обновим anchor, чтобы повторный Sync не затёр пользовательскую правку
            _lastSyncedFrom = state.SerializeWorking(Newtonsoft.Json.Formatting.Indented);
            _buffer = _lastSyncedFrom;
            _lastError = null;
        }

        private static bool TryParse(string s, out JArray arr)
        {
            try
            {
                var token = JToken.Parse(string.IsNullOrWhiteSpace(s) ? "[]" : s);
                if (token is JArray ja)
                {
                    arr = ja;
                    return true;
                }
                arr = null;
                return false;
            }
            catch (Exception)
            {
                arr = null;
                return false;
            }
        }
    }
}
