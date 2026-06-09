using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Configs.Editor
{
    /// <summary>
    /// Unity Editor-окно правки серверных конфигов через admin API.
    /// Phase A: Pull/Publish/Bootstrap/Conflict (Raw JSON).
    /// Phase B: список items слева + typed (books) / generic drawer + валидация.
    /// History/Rollback/Promote — Phase C. Спека: docs/CONFIG_EDITOR_WINDOW_MVP_SPEC.md.
    /// </summary>
    internal sealed class ConfigEditorWindow : EditorWindow
    {
        private static readonly string[] Sections = { "books", "locations", "requests", "events" };
        private static readonly string[] Environments = { "dev", "prod" };

        private const string BootstrapEtag = "bootstrap";

        private readonly SectionState _state = new();
        private readonly AdminApiClient _api = new();
        private readonly RawJsonDrawer _rawDrawer = new();
        private readonly GenericItemDrawer _genericDrawer = new();
        private readonly ItemListPanel _itemList = new();

        private bool _connectionFoldout = true;
        private bool _rawJsonMode;
        private string _baseUrlField;
        private string _userField;
        private string _passField;

        private Vector2 _detailScroll;
        private CancellationTokenSource _cts;

        [MenuItem("Tools/Configs/Editor Window")]
        public static void Open()
        {
            var w = GetWindow<ConfigEditorWindow>("Config Editor");
            w.minSize = new Vector2(820, 540);
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

            var issues = SectionValidator.Validate(_state.Section, _state.WorkingArray);
            var invalidIds = CollectInvalidIds(issues);

            using (new EditorGUI.DisabledScope(IsBusy()))
            {
                if (_rawJsonMode)
                {
                    DrawRawJsonPane();
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    DrawLeftPane(invalidIds);
                    DrawRightPane();
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space(4);
            UpdateDirtyTransition();
            DrawBottomPanel(issues);
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

            using (new EditorGUI.DisabledScope(!CanShowHistory()))
                if (GUILayout.Button("History", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    ConfigHistoryWindow.Open(_state.Section, _state.Environment, OnRolledBack);

            using (new EditorGUI.DisabledScope(!CanPromote()))
                if (GUILayout.Button("Promote to Prod", EditorStyles.toolbarButton, GUILayout.Width(120)))
                    PromoteAsync().Forget();

            var rawNext = GUILayout.Toggle(_rawJsonMode, "Raw JSON", EditorStyles.toolbarButton, GUILayout.Width(80));
            if (rawNext != _rawJsonMode)
            {
                _rawJsonMode = rawNext;
                _rawDrawer.Sync(_state); // при входе в Raw JSON синхронизируем буфер с working
            }

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

        private void DrawLeftPane(HashSet<string> invalidIds)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(260));
            _itemList.Draw(_state, invalidIds);
            EditorGUILayout.EndVertical();
        }

        private void DrawRightPane()
        {
            EditorGUILayout.BeginVertical();
            var selected = FindSelectedItem();
            if (selected == null)
            {
                EditorGUILayout.HelpBox(
                    _state.WorkingArray.Count == 0
                        ? "Section is empty. Click Add to create the first item."
                        : "Select an item on the left to edit.",
                    MessageType.Info);
            }
            else
            {
                _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll, GUILayout.ExpandHeight(true));
                if (_state.Section == "books")
                    BooksItemDrawer.Draw(selected);
                else
                    _genericDrawer.Draw(selected);
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawRawJsonPane()
        {
            _rawDrawer.Sync(_state);
            EditorGUILayout.LabelField("Raw JSON (whole section)", EditorStyles.boldLabel);
            _rawDrawer.Draw(_state);
        }

        private void DrawBottomPanel(IReadOnlyList<ValidationIssue> issues)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Publish Comment", EditorStyles.boldLabel, GUILayout.Width(160));
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(_state.WorkingArray == null || _state.WorkingArray.Count == 0))
            {
                if (GUILayout.Button("Copy Current JSON", GUILayout.Width(150)))
                {
                    EditorGUIUtility.systemCopyBuffer = _state.SerializeWorking(Newtonsoft.Json.Formatting.Indented);
                    _state.LastOperationResult = "Working JSON copied to clipboard.";
                }
            }
            EditorGUILayout.EndHorizontal();
            _state.PublishComment = EditorGUILayout.TextField(_state.PublishComment ?? string.Empty);

            if (issues.Count > 0)
            {
                var msg = "Validation issues (Publish disabled):\n - " + string.Join(
                    "\n - ",
                    issues.ConvertAllToStrings());
                EditorGUILayout.HelpBox(msg, MessageType.Warning);
            }

            if (!string.IsNullOrEmpty(_state.LastOperationResult))
                EditorGUILayout.HelpBox(_state.LastOperationResult, MessageType.Info);

            if (!string.IsNullOrEmpty(_state.LastError))
            {
                EditorGUILayout.HelpBox(_state.LastError, MessageType.Error);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Copy Error", GUILayout.Width(100)))
                {
                    EditorGUIUtility.systemCopyBuffer = _state.LastError;
                    _state.LastOperationResult = "Last error copied to clipboard.";
                }
                EditorGUILayout.EndHorizontal();
            }
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

                var etag = !string.IsNullOrEmpty(res.ETag)
                    ? res.ETag
                    : Game.Configs.Server.Commands.GetConfigCommand.NormalizeEtag(dto.Etag);
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
                _state.LastError = "Validation failed:\n - " + string.Join("\n - ", issues.ConvertAllToStrings());
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

                var etag = !string.IsNullOrEmpty(res.ETag)
                    ? res.ETag
                    : Game.Configs.Server.Commands.GetConfigCommand.NormalizeEtag(dto.Etag);
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

        private async UniTask PromoteAsync()
        {
            ResetMessages();

            if (!EditorUtility.DisplayDialog(
                "Promote to Prod",
                $"Promote section '{_state.Section}' from dev to prod? " +
                "The current dev version will be copied byte-for-byte into prod as a new version.",
                "Promote", "Cancel"))
                return;

            _state.State = EditorWindowState.Publishing;
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            try
            {
                var url = _api.PromoteUrl(_state.Section, "dev", "prod");
                var res = await _api.PostAsync(url, _cts.Token);

                if (res.StatusCode == 401)
                {
                    _state.LastError = "401 Unauthorized — проверь Username/Password.";
                    _state.State = EditorWindowState.Error;
                    Repaint();
                    return;
                }
                if (!res.Success)
                {
                    _state.LastError = $"Promote failed: HTTP {res.StatusCode}: {res.Error}";
                    _state.State = EditorWindowState.Error;
                    Repaint();
                    return;
                }

                _state.LastOperationResult = "Promoted to prod.";
                _state.State = EditorWindowState.Loaded;
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

        // Колбек после успешного rollback из ConfigHistoryWindow: подтянуть свежую версию в главное окно.
        private void OnRolledBack()
        {
            PullAsync().Forget();
            TryClearServerSnapshot();
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
            if (_state.State != EditorWindowState.Loaded && _state.State != EditorWindowState.Empty && _state.State != EditorWindowState.Dirty)
                return false;
            // Блок при наличии validation issues — определяется по живому валидатору в OnGUI.
            var issues = SectionValidator.Validate(_state.Section, _state.WorkingArray);
            if (issues.Count > 0) return false;
            return _state.IsDirty || _state.State == EditorWindowState.Empty;
        }

        // §12: History — enabled when section loaded.
        private bool CanShowHistory()
            => ConfigEditorSettings.IsConfigured
               && !IsBusy()
               && (_state.State == EditorWindowState.Loaded || _state.State == EditorWindowState.Dirty);

        // §12: Promote to Prod — enabled only for dev (и когда есть что promote'ить).
        private bool CanPromote()
            => ConfigEditorSettings.IsConfigured
               && !IsBusy()
               && _state.Environment == "dev"
               && _state.State == EditorWindowState.Loaded
               && !_state.IsDirty; // promote копирует серверную версию — лучше требовать чистого состояния, иначе ГД может думать, что promote'нул свои локальные правки.

        private void UpdateDirtyTransition()
        {
            if (_state.IsDirty && _state.State == EditorWindowState.Loaded)
                _state.State = EditorWindowState.Dirty;
            else if (!_state.IsDirty && _state.State == EditorWindowState.Dirty)
                _state.State = EditorWindowState.Loaded;
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

        private JObject FindSelectedItem() => _state.SelectedItem;

        private static HashSet<string> CollectInvalidIds(IReadOnlyList<ValidationIssue> issues)
        {
            var set = new HashSet<string>();
            for (var i = 0; i < issues.Count; i++)
                if (!string.IsNullOrEmpty(issues[i].ItemId))
                    set.Add(issues[i].ItemId);
            return set;
        }
    }

    internal static class ValidationIssueExtensions
    {
        public static List<string> ConvertAllToStrings(this IReadOnlyList<ValidationIssue> issues)
        {
            var list = new List<string>(issues.Count);
            for (var i = 0; i < issues.Count; i++) list.Add(issues[i].Message);
            return list;
        }
    }
}
