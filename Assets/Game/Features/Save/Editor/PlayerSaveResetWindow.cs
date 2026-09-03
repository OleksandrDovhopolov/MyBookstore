using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.AdminTooling.Editor;
using Save.Identity;
using UnityEditor;
using UnityEngine;

namespace Save.Editor
{
    /// <summary>
    /// Tools/Save/Reset Player Server Save.
    ///
    /// Calls POST /api/admin/test/player/{playerId}/save/reset. Deliberately does NOT run the auth
    /// flow and never creates a player id: in Current Auth Player mode it only reads whatever
    /// <see cref="AuthPlayerId.PlayerPrefsKey"/> already holds, and refuses to act if it is missing.
    /// </summary>
    public sealed class PlayerSaveResetWindow : EditorWindow
    {
        private enum Mode
        {
            ManualPlayerId = 0,
            CurrentAuthPlayer = 1
        }

        private static readonly string[] ModeLabels = { "Manual Player ID", "Current Auth Player" };

        /// <summary>
        /// Locally generated install id used by HttpSaveStorage as the player id today. Shown only as a
        /// hint — auto mode deliberately never falls back to it (see the window summary).
        /// </summary>
        private const string LegacyInstallPlayerIdKey = "save.http.player_id.v1";

        private readonly PlayerSaveResetAdminClient _client = new();

        private Mode _mode = Mode.ManualPlayerId;
        private bool _connectionFoldout = true;

        private string _baseUrlField;
        private string _userField;
        private string _passField;
        private string _manualPlayerId = string.Empty;

        private string _autoPlayerId = string.Empty;
        private string _autoPlayerIdError;

        private CancellationTokenSource _cts;
        private bool _isBusy;
        private string _status;
        private MessageType _statusType = MessageType.None;

        [MenuItem("Tools/Save/Reset Player Server Save")]
        private static void Open()
        {
            var window = GetWindow<PlayerSaveResetWindow>(utility: false, title: "Reset Player Save");
            window.minSize = new Vector2(460f, 320f);
            window.Show();
        }

        private void OnEnable()
        {
            _baseUrlField = AdminApiSettings.BaseUrl;
            _userField = AdminApiSettings.Username;
            _passField = AdminApiSettings.Password;
            RefreshAutoPlayerId();
        }

        private void OnDisable()
        {
            // Window closed mid-request: abort it rather than leaving a callback pointing at a dead window.
            CancelInFlight();
        }

        private void OnGUI()
        {
            DrawConnectionFoldout();
            EditorGUILayout.Space();

            _mode = (Mode)GUILayout.Toolbar((int)_mode, ModeLabels);
            EditorGUILayout.Space();

            var playerId = _mode == Mode.ManualPlayerId ? DrawManualMode() : DrawAutoMode();

            EditorGUILayout.Space();
            DrawResetButton(playerId);
            DrawStatus();
        }

        private void DrawConnectionFoldout()
        {
            _connectionFoldout = EditorGUILayout.Foldout(_connectionFoldout, "Connection", true);
            if (!_connectionFoldout)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                _baseUrlField = EditorGUILayout.TextField("Base URL", _baseUrlField);
                _userField = EditorGUILayout.TextField("Username", _userField);
                _passField = EditorGUILayout.PasswordField("Password", _passField);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Save Locally", GUILayout.Width(120)))
                {
                    AdminApiSettings.BaseUrl = _baseUrlField;
                    AdminApiSettings.Username = _userField;
                    AdminApiSettings.Password = _passField;
                    SetStatus("Credentials saved to EditorPrefs (per-machine, shared with Config Editor).",
                        MessageType.Info);
                }

                using (new EditorGUI.DisabledScope(!AdminApiSettings.IsConfigured || _isBusy))
                {
                    if (GUILayout.Button("Test Connection", GUILayout.Width(140)))
                    {
                        TestConnectionAsync().Forget();
                    }
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                if (!AdminApiSettings.IsConfigured)
                {
                    EditorGUILayout.HelpBox(
                        "Base URL, Username and Password must be saved before a reset can run.",
                        MessageType.Warning);
                }
            }
        }

        /// <summary>Manual mode accepts any non-empty id — server test players like "p1" are valid.</summary>
        private string DrawManualMode()
        {
            _manualPlayerId = EditorGUILayout.TextField("Player ID", _manualPlayerId);
            EditorGUILayout.HelpBox(
                "Any server-known player id, e.g. \"p1\" or a GUID. Not validated as a GUID here.",
                MessageType.None);
            return _manualPlayerId?.Trim() ?? string.Empty;
        }

        private string DrawAutoMode()
        {
            EditorGUILayout.LabelField($"PlayerPrefs key: {AuthPlayerId.PlayerPrefsKey}", EditorStyles.miniLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Auth Player ID",
                    string.IsNullOrEmpty(_autoPlayerId) ? "<not set>" : _autoPlayerId);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Width(100)))
            {
                RefreshAutoPlayerId();
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_autoPlayerId)))
            {
                if (GUILayout.Button("Copy", GUILayout.Width(100)))
                {
                    EditorGUIUtility.systemCopyBuffer = _autoPlayerId;
                    SetStatus("Auth player id copied to clipboard.", MessageType.Info);
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (_autoPlayerIdError != null)
            {
                EditorGUILayout.HelpBox(_autoPlayerIdError, MessageType.Error);
                DrawLegacyInstallIdHint();
                return string.Empty;
            }

            return _autoPlayerId;
        }

        /// <summary>
        /// Read-only view of the legacy install id, with a one-click hand-off to Manual mode. Auto mode
        /// itself never uses this value — copying it is an explicit, visible act.
        /// </summary>
        private void DrawLegacyInstallIdHint()
        {
            var legacy = PlayerPrefs.GetString(LegacyInstallPlayerIdKey, string.Empty);
            if (string.IsNullOrWhiteSpace(legacy))
            {
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Install ID (legacy)", legacy);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Use in Manual mode", GUILayout.Width(160)))
            {
                _manualPlayerId = legacy;
                _mode = Mode.ManualPlayerId;
                GUI.FocusControl(null);
                SetStatus("Install id copied into Manual Player ID.", MessageType.Info);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawResetButton(string playerId)
        {
            var canReset = !_isBusy
                           && AdminApiSettings.IsConfigured
                           && !string.IsNullOrWhiteSpace(playerId);

            using (new EditorGUI.DisabledScope(!canReset))
            {
                if (GUILayout.Button(_isBusy ? "Resetting..." : "Reset Player Save", GUILayout.Height(28f)))
                {
                    if (ConfirmReset(playerId))
                    {
                        ResetAsync(playerId).Forget();
                    }
                }
            }
        }

        private void DrawStatus()
        {
            if (string.IsNullOrEmpty(_status))
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(_status, _statusType);
        }

        private static bool ConfirmReset(string playerId)
        {
            return EditorUtility.DisplayDialog(
                "Reset player save?",
                $"Player: {playerId}\n\n" +
                "This wipes the player's save and progress on the server.\n" +
                "The account, auth tokens and purchases are NOT deleted.\n\n" +
                "This cannot be undone.",
                "Reset",
                "Cancel");
        }

        private void RefreshAutoPlayerId()
        {
            var raw = AuthPlayerId.ReadRaw();
            _autoPlayerId = raw ?? string.Empty;

            if (string.IsNullOrWhiteSpace(raw))
            {
                // Nothing writes this key yet — the auth flow (/api/v1/auth/anonymous) is not built.
                // Saying "run the game" here would be a lie: playing does not populate it.
                _autoPlayerIdError =
                    $"PlayerPrefs key '{AuthPlayerId.PlayerPrefsKey}' is empty.\n" +
                    "No code writes it yet — the anonymous auth flow is not implemented, so running " +
                    "the game will not populate it.\n" +
                    "Use Manual Player ID for now. The id the server currently knows is the install id " +
                    $"stored under '{LegacyInstallPlayerIdKey}'.";
                return;
            }

            if (!AuthPlayerId.IsValid(raw))
            {
                _autoPlayerIdError =
                    $"Stored value is not a valid player id (expected a GUID with no dashes): '{raw}'.";
                return;
            }

            _autoPlayerIdError = null;
        }

        /// <summary>
        /// Read-only probe of GET /api/admin/player/{id}. 404 counts as success: it proves the
        /// credentials were accepted and only says this particular player is unknown to the server.
        /// </summary>
        private async UniTaskVoid TestConnectionAsync()
        {
            CancelInFlight();
            _cts = new CancellationTokenSource();

            // Probe with whatever id is on screen so the result also tells you whether that player
            // exists; fall back to a harmless placeholder when no id is entered yet.
            var probeId = CurrentPlayerId();
            var usingPlaceholder = string.IsNullOrWhiteSpace(probeId);
            if (usingPlaceholder)
            {
                probeId = "connection-test";
            }

            _isBusy = true;
            SetStatus("Testing connection...", MessageType.Info);

            try
            {
                var result = await _client.TestConnectionAsync(probeId, _cts.Token);

                if (result.Canceled)
                {
                    SetStatus("Connection test canceled.", MessageType.Warning);
                }
                else if (result.Success)
                {
                    SetStatus($"Connection OK (HTTP {result.StatusCode}). Player '{probeId}' exists on the server.",
                        MessageType.Info);
                }
                else if (result.StatusCode == 404)
                {
                    SetStatus(usingPlaceholder
                            ? "Connection OK (HTTP 404) — credentials accepted."
                            : $"Connection OK (HTTP 404) — credentials accepted, but the server does not know player '{probeId}'.",
                        MessageType.Info);
                }
                else
                {
                    SetStatus($"HTTP {result.StatusCode}\n{result.Error}", MessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Connection test failed: {ex.Message}", MessageType.Error);
            }
            finally
            {
                _isBusy = false;
                Repaint();
            }
        }

        /// <summary>Player id for the active mode, or empty when it is missing/invalid.</summary>
        private string CurrentPlayerId()
        {
            if (_mode == Mode.ManualPlayerId)
            {
                return _manualPlayerId?.Trim() ?? string.Empty;
            }

            return _autoPlayerIdError == null ? _autoPlayerId : string.Empty;
        }

        private async UniTaskVoid ResetAsync(string playerId)
        {
            CancelInFlight();
            _cts = new CancellationTokenSource();

            _isBusy = true;
            SetStatus($"Resetting save for '{playerId}'...", MessageType.Info);

            try
            {
                var result = await _client.ResetAsync(playerId, _cts.Token);

                if (result.Canceled)
                {
                    SetStatus("Reset canceled.", MessageType.Warning);
                    return;
                }

                if (result.Success)
                {
                    OnResetSucceeded(playerId, result.StatusCode);
                    return;
                }

                SetStatus($"HTTP {result.StatusCode}\n{result.Error}", MessageType.Error);
            }
            catch (Exception ex)
            {
                SetStatus($"Reset failed: {ex.Message}", MessageType.Error);
            }
            finally
            {
                _isBusy = false;
                Repaint();
            }
        }

        private void OnResetSucceeded(string playerId, long statusCode)
        {
            var message = $"HTTP {statusCode} — server save reset for '{playerId}'.";

            // Only the current auth player's local cache corresponds to what was just wiped server-side.
            // Deleting it for an arbitrary manual id would destroy an unrelated local save.
            if (IsCurrentAuthPlayer(playerId))
            {
                var deleted = LocalSaveFiles.DeleteAll();
                message += deleted.Count == 0
                    ? "\nNo local save files to delete."
                    : $"\nDeleted local save: {string.Join(", ", deleted)}";
            }
            else
            {
                message += "\nLocal save left untouched (not the current auth player).";
            }

            SetStatus(message, MessageType.Info);
        }

        private static bool IsCurrentAuthPlayer(string playerId)
            => AuthPlayerId.TryRead(out var current)
               && string.Equals(current, playerId, StringComparison.OrdinalIgnoreCase);

        private void CancelInFlight()
        {
            if (_cts == null)
            {
                return;
            }

            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        private void SetStatus(string message, MessageType type)
        {
            _status = message;
            _statusType = type;
            Repaint();
        }
    }
}
