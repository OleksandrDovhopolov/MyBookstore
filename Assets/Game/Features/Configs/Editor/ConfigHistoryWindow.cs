using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Game.Configs.Editor
{
    /// <summary>
    /// Окно истории секции (§4.1, §8.9–§8.10 спеки). View JSON — lazy: при первом клике
    /// загружает контент через GET /versions/{v} (CONFIG_SERVER_API.md §3.6) и кэширует
    /// в пределах жизни окна.
    /// </summary>
    internal sealed class ConfigHistoryWindow : EditorWindow
    {
        private const int RowsHeight = 22;

        private string _section;
        private string _environment;
        private Action _onRolledBack;

        private readonly AdminApiClient _api = new();
        private List<HistoryEntryDto> _entries;
        private readonly Dictionary<long, string> _jsonCache = new();      // version → pretty JSON
        private readonly HashSet<long> _fetching = new();                  // versions currently loading
        private string _error;
        private string _info;
        private bool _loading;
        private long _expandedVersion = -1;
        private Vector2 _scroll;
        private CancellationTokenSource _cts;

        public static void Open(string section, string environment, Action onRolledBack)
        {
            var w = CreateInstance<ConfigHistoryWindow>();
            w.titleContent = new GUIContent($"History — {section}/{environment}");
            w.minSize = new Vector2(720, 360);
            w._section = section;
            w._environment = environment;
            w._onRolledBack = onRolledBack;
            w.ShowUtility();
            w.LoadAsync().Forget();
        }

        private void OnDisable()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField($"{_section} / {_environment}", EditorStyles.toolbarButton, GUILayout.Width(220));
            using (new EditorGUI.DisabledScope(_loading))
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    LoadAsync().Forget();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_info)) EditorGUILayout.HelpBox(_info, MessageType.Info);
            if (!string.IsNullOrEmpty(_error)) EditorGUILayout.HelpBox(_error, MessageType.Error);

            if (_loading)
            {
                EditorGUILayout.LabelField("Loading…");
                return;
            }
            if (_entries == null) return;

            DrawHeader();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var entry in _entries)
                DrawRow(entry);
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            HeaderCell("Version", 70);
            HeaderCell("ETag", 110);
            HeaderCell("UpdatedBy", 140);
            HeaderCell("UpdatedAt", 160);
            HeaderCell("Comment", 200);
            GUILayout.FlexibleSpace();
            HeaderCell("Actions", 160);
            EditorGUILayout.EndHorizontal();
        }

        private static void HeaderCell(string text, float w)
            => EditorGUILayout.LabelField(text, EditorStyles.toolbarButton, GUILayout.Width(w));

        private void DrawRow(HistoryEntryDto entry)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Height(RowsHeight));
            EditorGUILayout.LabelField(entry.Version.ToString(), GUILayout.Width(70));
            EditorGUILayout.LabelField(Short(entry.Etag), GUILayout.Width(110));
            EditorGUILayout.LabelField(entry.UpdatedBy ?? string.Empty, GUILayout.Width(140));
            EditorGUILayout.LabelField(entry.UpdatedAt.ToString("u"), GUILayout.Width(160));
            EditorGUILayout.LabelField(entry.Comment ?? string.Empty, GUILayout.Width(200));
            GUILayout.FlexibleSpace();

            var isFetching = _fetching.Contains(entry.Version);
            var hasCached = _jsonCache.ContainsKey(entry.Version);
            var btnLabel = isFetching
                ? "Loading…"
                : (_expandedVersion == entry.Version ? "Hide JSON" : "View JSON");

            using (new EditorGUI.DisabledScope(isFetching || _loading))
            {
                if (GUILayout.Button(btnLabel, GUILayout.Width(80)))
                {
                    if (_expandedVersion == entry.Version)
                    {
                        _expandedVersion = -1;
                    }
                    else if (hasCached)
                    {
                        _expandedVersion = entry.Version;
                    }
                    else
                    {
                        FetchVersionAsync(entry.Version).Forget();
                    }
                }
            }
            using (new EditorGUI.DisabledScope(_loading))
            {
                if (GUILayout.Button("Rollback", GUILayout.Width(70)))
                    RollbackAsync(entry.Version).Forget();
            }
            EditorGUILayout.EndHorizontal();

            if (_expandedVersion == entry.Version && hasCached)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.TextArea(_jsonCache[entry.Version], GUILayout.MinHeight(120));
                }
            }
        }

        private async UniTask LoadAsync()
        {
            _error = null;
            _info = null;
            _loading = true;
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            try
            {
                var url = _api.HistoryUrl(_section, _environment);
                var res = await _api.GetAsync(url, _cts.Token);
                if (!res.Success)
                {
                    _error = $"History failed: HTTP {res.StatusCode}: {res.Error}";
                    _entries = null;
                    return;
                }

                try { _entries = JsonConvert.DeserializeObject<List<HistoryEntryDto>>(res.Body); }
                catch (Exception ex)
                {
                    _error = $"History: cannot parse server response: {ex.Message}";
                    _entries = null;
                    return;
                }
                if (_entries == null || _entries.Count == 0)
                    _info = "No history entries yet.";

                // Кэш контента сбрасываем при обновлении истории (после rollback версия номер 6 может означать другое).
                _jsonCache.Clear();
                _fetching.Clear();
                _expandedVersion = -1;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { _error = ex.Message; }
            finally
            {
                _loading = false;
                Repaint();
            }
        }

        private async UniTask FetchVersionAsync(long version)
        {
            if (_fetching.Contains(version) || _jsonCache.ContainsKey(version))
                return;

            _fetching.Add(version);
            _error = null;
            Repaint();

            var cts = _cts ??= new CancellationTokenSource();
            try
            {
                var url = _api.VersionUrl(_section, _environment, version);
                var res = await _api.GetAsync(url, cts.Token);
                if (!res.Success)
                {
                    _error = $"View JSON v{version} failed: HTTP {res.StatusCode}: {res.Error}";
                    return;
                }

                // Сервер должен вернуть тот же shape, что admin GET текущей версии (§3.6).
                AdminConfigDto dto;
                try { dto = JsonConvert.DeserializeObject<AdminConfigDto>(res.Body); }
                catch (Exception ex)
                {
                    _error = $"View JSON v{version}: cannot parse response: {ex.Message}";
                    return;
                }

                var jsonText = dto?.Json != null
                    ? dto.Json.ToString(Formatting.Indented)
                    : res.Body; // fallback: если сервер прислал чистый массив — покажем raw.

                _jsonCache[version] = jsonText;
                _expandedVersion = version;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { _error = ex.Message; }
            finally
            {
                _fetching.Remove(version);
                Repaint();
            }
        }

        private async UniTask RollbackAsync(long toVersion)
        {
            if (!EditorUtility.DisplayDialog(
                "Rollback",
                $"Rollback section '{_section}/{_environment}' to version {toVersion}? " +
                "This creates a new version identical to the selected one (history is preserved).",
                "Rollback", "Cancel"))
                return;

            _error = null;
            _info = null;
            _loading = true;
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            try
            {
                var url = _api.RollbackUrl(_section, _environment, toVersion);
                var res = await _api.PostAsync(url, _cts.Token);
                if (!res.Success)
                {
                    _error = $"Rollback failed: HTTP {res.StatusCode}: {res.Error}";
                    return;
                }
                _info = $"Rolled back to v{toVersion}. Main window will refresh.";
                _onRolledBack?.Invoke();
                await LoadAsync(); // обновить таблицу — появилась новая запись (current = копия toVersion)
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { _error = ex.Message; }
            finally
            {
                _loading = false;
                Repaint();
            }
        }

        private static string Short(string s)
            => string.IsNullOrEmpty(s) ? "—" : (s.Length > 14 ? s.Substring(0, 14) + "…" : s);
    }
}
