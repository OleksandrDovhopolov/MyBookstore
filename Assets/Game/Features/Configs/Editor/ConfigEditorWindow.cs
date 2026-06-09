using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Configs.Editor
{
    /// <summary>
    /// Unity Editor-окно для правки серверных конфигов через admin API.
    /// Phase A: Pull / Publish / 404 (empty) / 412 (conflict) / 200, правая панель — Raw JSON only.
    /// Список items, typed/generic-формы, History/Rollback/Promote — Phase B/C.
    /// Спека: docs/CONFIG_EDITOR_WINDOW_MVP_SPEC.md.
    /// </summary>
    internal sealed class ConfigEditorWindow : EditorWindow
    {
        private static readonly string[] Sections = { "books", "locations", "requests", "events" };
        private static readonly string[] Environments = { "dev", "prod" };

        private const string BootstrapEtag = "bootstrap";

        private readonly SectionState _state = new();
        private readonly AdminApiClient _api = new();
        private readonly RawJsonDrawer _rawDrawer = new();

        private bool _connectionFoldout = true;
        private string _baseUrlField;
        private string _userField;
        private string _passField;

        private CancellationTokenSource _cts;

        [MenuItem("Tools/Configs/Editor Window")]
        public static void Open()
        {
            var w = GetWindow<ConfigEditorWindow>("Config Editor");
            w.minSize = new Vector2(720, 520);
            w.Show();
        }

        private void OnEnable()
        {
            _baseUrlField = ConfigEditorSettings.BaseUrl;
            _userField = ConfigEditorSettings.Username;
            _passField = ConfigEditorSettings.Password;
            RefreshDisconnectedState();
        }

        private void OnDisable()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        // ----- UI -----

        private void OnGUI()
        {
            DrawToolbar();
            DrawConnectionFoldout();
            EditorGUILayout.Space(4);

            using (new EditorGUI.DisabledScope(IsBusy()))
                DrawRightPane();

            EditorGUILayout.Space(4);
            DrawBottomPanel();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            var newSection = EditorGUILayout.Popup(Array.IndexOf(Sections, _state.Section), Sections, EditorStyles.toolbarPopup, GUILayout.Width(110));
            if (newSection >= 0 && Sections[newSection] != _state.Section)
            {
                _state.Section = Sections[newSection];
                _state.MarkEmpty();
                _state.State = ConfigEditorSettings.IsConfigured ? EditorWindowState.Idle : EditorWindowState.Disconnected;
            }

            var newEnv = EditorGUILayout.Popup(Array.IndexOf(Environments, _state.Environment), Environments, EditorStyles.toolbarPopup, GUILayout.Width(80));
            if (newEnv >= 0 && Environments[newEnv] != _state.Environment)
            {
                _state.Environment = Environments[newEnv];
                _state.MarkEmpty();
                _state.State = ConfigEditorSettings.IsConfigured ? EditorWindowState.Idle : EditorWindowState.Disconnected;
            }

            using (new EditorGUI.DisabledScope(!ConfigEditorSettings.IsConfigured || IsBusy()))
                if (GUILayout.Button("Pull", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    PullAsync().Forget();

            using (new EditorGUI.DisabledScope(!CanPublish()))
                if (GUILayout.Button("Publish", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    PublishAsync().Forget();

            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField(StateLabel(), EditorStyles.toolbarTextField, GUILayout.Width(360));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawConnectionFoldout()
        {
            _connectionFoldout = EditorGUILayout.Foldout(_connectionFoldout, "Connection", true);
            if (!_connectionFoldout) return;

            using (new EditorGUI.IndentLevelScope())
            {
                _baseUrlField = EditorGUILayout.TextField("Base URL", _baseUrlField);
                _userField = EditorGUILayout.TextField("Username", _userField);
                _passField = EditorGUILayout.PasswordField("Password", _passField);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Save Locally", GUILayout.Width(120)))
                {
                    ConfigEditorSettings.BaseUrl = _baseUrlField;
                    ConfigEditorSettings.Username = _userField;
                    ConfigEditorSettings.Password = _passField;
                    RefreshDisconnectedState();
                    _state.LastOperationResult = "Credentials saved to EditorPrefs (per-machine).";
                }
                using (new EditorGUI.DisabledScope(!ConfigEditorSettings.IsConfigured || IsBusy()))
                    if (GUILayout.Button("Test Connection", GUILayout.Width(140)))
                        TestConnectionAsync().Forget();

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawRightPane()
        {
            _rawDrawer.Sync(_state);
            EditorGUILayout.LabelField("Raw JSON (Phase A — typed/generic forms TBD in Phase B)", EditorStyles.boldLabel);
            _rawDrawer.Draw(_state);

            // Подсветка Dirty в state-машине (§11). Pulled snapshot → правка → Dirty.
            if (_state.IsDirty && _state.State == EditorWindowState.Loaded)
                _state.State = EditorWindowState.Dirty;
            else if (!_state.IsDirty && _state.State == EditorWindowState.Dirty)
                _state.State = EditorWindowState.Loaded;
        }

        private void DrawBottomPanel()
        {
            EditorGUILayout.LabelField("Publish Comment", EditorStyles.boldLabel);
            _state.PublishComment = EditorGUILayout.TextField(_state.PublishComment ?? string.Empty);

            if (!string.IsNullOrEmpty(_state.LastOperationResult))
                EditorGUILayout.HelpBox(_state.LastOperationResult, MessageType.Info);

            if (!string.IsNullOrEmpty(_state.LastError))
                EditorGUILayout.HelpBox(_state.LastError, MessageType.Error);
        }

        // ----- Operations -----

        private async UniTask TestConnectionAsync()
        {
            ResetMessages();
            _state.State = EditorWindowState.Loading;
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            try
            {
                var url = _api.SectionUrl(_state.Section, _state.Environment);
                var res = await _api.GetAsync(url, _cts.Token);
                if (res.Success || res.StatusCode == 404)
                {
                    _state.LastOperationResult = $"Test connection OK (HTTP {res.StatusCode}).";
                    _state.State = ConfigEditorSettings.IsConfigured ? EditorWindowState.Idle : EditorWindowState.Disconnected;
                }
                else if (res.StatusCode == 401)
                {
                    _state.LastError = "401 Unauthorized — проверь Username/Password.";
                    _state.State = EditorWindowState.Error;
                }
                else
                {
                    _state.LastError = $"HTTP {res.StatusCode}: {res.Error}";
                    _state.State = EditorWindowState.Error;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _state.LastError = ex.Message;
                _state.State = EditorWindowState.Error;
            }
            Repaint();
        }

        private async UniTask PullAsync()
        {
            ResetMessages();
            _state.State = EditorWindowState.Loading;
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            try
            {
                var url = _api.SectionUrl(_state.Section, _state.Environment);
                var res = await _api.GetAsync(url, _cts.Token);

                if (res.StatusCode == 404)
                {
                    _state.MarkEmpty();
                    _state.State = EditorWindowState.Empty;
                    _state.LastOperationResult = $"Section '{_state.Section}/{_state.Environment}' is empty. First Publish will bootstrap it.";
                    Repaint();
                    return;
                }
                if (res.StatusCode == 401)
                {
                    _state.LastError = "401 Unauthorized — проверь Username/Password.";
                    _state.State = EditorWindowState.Error;
                    Repaint();
                    return;
                }
                if (!res.Success)
                {
                    _state.LastError = $"Pull failed: HTTP {res.StatusCode}: {res.Error}";
                    _state.State = EditorWindowState.Error;
                    Repaint();
                    return;
                }

                AdminConfigDto dto;
                try { dto = JsonConvert.DeserializeObject<AdminConfigDto>(res.Body); }
                catch (Exception ex)
                {
                    _state.LastError = $"Pull: cannot parse server response: {ex.Message}";
                    _state.State = EditorWindowState.Error;
                    Repaint();
                    return;
                }

                var etag = !string.IsNullOrEmpty(res.ETag) ? res.ETag : Game.Configs.Server.Commands.GetConfigCommand.NormalizeEtag(dto.Etag);
                _state.ApplyPulled(dto, etag);
                _state.State = EditorWindowState.Loaded;
                _state.LastOperationResult = $"Pulled '{_state.Section}/{_state.Environment}' version={dto.Version}.";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _state.LastError = ex.Message;
                _state.State = EditorWindowState.Error;
            }
            Repaint();
        }

        private async UniTask PublishAsync()
        {
            ResetMessages();

            var issues = SectionValidator.Validate(_state.Section, _state.WorkingArray);
            if (issues.Count > 0)
            {
                _state.LastError = "Validation failed:\n - " + string.Join("\n - ", issues.ConvertAll(i => i.Message));
                _state.State = EditorWindowState.Error;
                Repaint();
                return;
            }

            _state.State = EditorWindowState.Publishing;
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            try
            {
                var url = _api.SectionUrl(_state.Section, _state.Environment);
                var ifMatch = !string.IsNullOrEmpty(_state.CurrentEtag) ? _state.CurrentEtag : BootstrapEtag;
                var body = new JObject
                {
                    ["json"] = _state.WorkingArray,
                    ["comment"] = _state.PublishComment ?? string.Empty
                }.ToString(Formatting.None);

                var res = await _api.PutAsync(url, body, ifMatch, _cts.Token);

                if (res.StatusCode == 412)
                {
                    _state.State = EditorWindowState.Conflict;
                    _state.LastError = "412 Precondition Failed — на сервере уже более новая версия.";
                    Repaint();
                    HandleConflict();
                    return;
                }
                if (res.StatusCode == 401)
                {
                    _state.LastError = "401 Unauthorized — проверь Username/Password.";
                    _state.State = EditorWindowState.Error;
                    Repaint();
                    return;
                }
                if (res.StatusCode == 400)
                {
                    _state.LastError = $"400 Bad Request: {res.Error}";
                    _state.State = EditorWindowState.Error;
                    Repaint();
                    return;
                }
                if (!res.Success)
                {
                    _state.LastError = $"Publish failed: HTTP {res.StatusCode}: {res.Error}";
                    _state.State = EditorWindowState.Error;
                    Repaint();
                    return;
                }

                AdminConfigDto dto;
                try { dto = JsonConvert.DeserializeObject<AdminConfigDto>(res.Body); }
                catch (Exception ex)
                {
                    _state.LastError = $"Publish: cannot parse server response: {ex.Message}";
                    _state.State = EditorWindowState.Error;
                    Repaint();
                    return;
                }

                var etag = !string.IsNullOrEmpty(res.ETag) ? res.ETag : Game.Configs.Server.Commands.GetConfigCommand.NormalizeEtag(dto.Etag);
                _state.ApplyPulled(dto, etag);
                _state.State = EditorWindowState.Loaded;
                _state.LastOperationResult = $"Published version={dto.Version} (etag={etag}).";
                TryClearServerSnapshot();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _state.LastError = ex.Message;
                _state.State = EditorWindowState.Error;
            }
            Repaint();
        }

        private void HandleConflict()
        {
            var reload = EditorUtility.DisplayDialog(
                "Conflict — newer version on server",
                "A newer version exists on the server. Reload Latest will discard your local changes and pull the current version. You will need to reapply changes manually.",
                "Reload Latest",
                "Cancel");
            if (reload)
                PullAsync().Forget();
        }

        // После успешного Publish — выбросить локальный server-snapshot, чтобы Play видел свежие данные.
        // Логика тождественна Tools/Configs/Clear Server Snapshot из ConfigsVContainerBindings.
        private void TryClearServerSnapshot()
        {
            try
            {
                var dir = System.IO.Path.Combine(Application.persistentDataPath, "configs");
                if (System.IO.Directory.Exists(dir))
                {
                    System.IO.Directory.Delete(dir, recursive: true);
                    _state.LastOperationResult += " Server snapshot cleared.";
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ConfigEditorWindow] Failed to clear server snapshot: {ex.Message}");
            }
        }

        // ----- Helpers -----

        private void ResetMessages()
        {
            _state.LastError = null;
            _state.LastOperationResult = null;
        }

        private bool IsBusy()
            => _state.State == EditorWindowState.Loading || _state.State == EditorWindowState.Publishing;

        private bool CanPublish()
        {
            if (!ConfigEditorSettings.IsConfigured) return false;
            if (IsBusy()) return false;
            // Loaded или Empty — можно публиковать; Empty=bootstrap. Dirty не обязателен для Loaded (повторная публикация без правок тоже валидна).
            if (_state.State != EditorWindowState.Loaded && _state.State != EditorWindowState.Empty && _state.State != EditorWindowState.Dirty)
                return false;
            return _state.IsDirty || _state.State == EditorWindowState.Empty;
        }

        private string StateLabel()
        {
            var dirty = _state.IsDirty ? " · Dirty" : string.Empty;
            return $"v{_state.CurrentVersion} · etag={Short(_state.CurrentEtag)} · {_state.State}{dirty}";
        }

        private static string Short(string s)
            => string.IsNullOrEmpty(s) ? "—" : (s.Length > 10 ? s.Substring(0, 10) + "…" : s);

        private void RefreshDisconnectedState()
        {
            _state.State = ConfigEditorSettings.IsConfigured ? EditorWindowState.Idle : EditorWindowState.Disconnected;
        }

        // Подсветить Dirty в state, когда working menyala (вызывается из drawers через Apply).
        // В Phase A правка идёт только через RawJsonDrawer.Apply, который обновляет working — а dirty считается через сравнение строк, так что отдельный hook не нужен.
    }
}
