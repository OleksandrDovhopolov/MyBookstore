using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands;
using Game.Configs.Server.Commands;
using Game.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.Configs.Server
{
    /// <summary>
    /// Runtime-источник: .NET сервер + disk snapshot fallback.
    /// Слои (от младшего к старшему): bundled defaults (fallback) → snapshot → server.
    /// Сеть упала → отдаём snapshot; снапшота нет → bundled defaults.
    ///
    /// ВНИМАНИЕ: публичные методы /configs/* на бэке ещё не реализованы — это seam,
    /// написанный против спека (docs/CONFIG_CACHE_SYSTEM.md, Phase 4). До выката
    /// бэкенда тестируется на моке (Postman/stub).
    /// </summary>
    public sealed class ServerConfigSource : IConfigSource
    {
        private const string LogPrefix = "[ServerConfigSource]";
        private const string ManifestFileName = "manifest.json";

        private readonly IConfigsBackendConfig _config;
        private readonly IConnectionService _connectionService;
        private readonly ICommandLogger _logger;
        private readonly ICommandErrorReporter _errorReporter;
        private readonly IConfigSource _bundledDefaults;

        private readonly string _snapshotDir;
        private readonly Dictionary<string, string> _rawByName = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _etagByName = new(StringComparer.OrdinalIgnoreCase);

        public ServerConfigSource(
            IConfigsBackendConfig config,
            IConnectionService connectionService,
            ICommandLogger logger,
            ICommandErrorReporter errorReporter,
            IConfigSource bundledDefaults)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _errorReporter = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));
            _bundledDefaults = bundledDefaults; // optional baseline (Assets/Configs in Editor)
            _snapshotDir = Path.Combine(Application.persistentDataPath, "configs");
        }

        public async UniTask WarmupAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _rawByName.Clear();
            _etagByName.Clear();

            // Baseline: bundled defaults (под GetRaw делегируем в _bundledDefaults, если нет в _rawByName).
            if (_bundledDefaults != null)
            {
                try { await _bundledDefaults.WarmupAsync(ct); }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Debug.LogWarning($"{LogPrefix} bundled defaults warmup failed: {ex.Message}");
                }
            }

            // Overlay: disk snapshot.
            LoadSnapshot();

            // Overlay: server (delta по ETag).
            var manifest = await TryFetchManifestAsync(ct);
            if (manifest == null)
            {
                Debug.LogWarning($"{LogPrefix} manifest unavailable — using snapshot/bundled defaults.");
                return;
            }

            foreach (var entry in manifest)
            {
                ct.ThrowIfCancellationRequested();
                await SyncOneAsync(entry, ct);
            }

            PersistManifest(manifest);
        }

        public string GetRaw(string fileName)
        {
            if (_rawByName.TryGetValue(fileName, out var raw))
                return raw;
            return _bundledDefaults?.GetRaw(fileName);
        }

        private async UniTask<List<ManifestEntry>> TryFetchManifestAsync(CancellationToken ct)
        {
            var url = Combine(_config.BaseUrl, _config.ManifestPath);
            var cmd = new GetConfigsManifestCommand(_connectionService, _logger, _errorReporter, url)
            {
                ConnectionCheckBehaviour = ConnectionCheckBehaviour.SilentWithComplete
            };

            await cmd.ExecuteAsync();
            ct.ThrowIfCancellationRequested();

            if (!cmd.IsSucceed || string.IsNullOrWhiteSpace(cmd.ManifestJson))
                return null;

            try
            {
                return JsonConvert.DeserializeObject<List<ManifestEntry>>(cmd.ManifestJson);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogPrefix} bad manifest JSON: {ex.Message}");
                return null;
            }
        }

        private async UniTask SyncOneAsync(ManifestEntry entry, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(entry.Name))
                return;

            var haveSnapshot = _rawByName.ContainsKey(entry.Name);
            _etagByName.TryGetValue(entry.Name, out var localEtag);

            // ETag совпал и файл уже есть локально → ничего не качаем.
            if (haveSnapshot && !string.IsNullOrEmpty(localEtag) && localEtag == entry.Etag)
                return;

            var url = Combine(_config.BaseUrl, string.Format(_config.ConfigPathFormat, entry.Name));
            var cmd = new GetConfigCommand(_connectionService, _logger, _errorReporter, url, localEtag)
            {
                ConnectionCheckBehaviour = ConnectionCheckBehaviour.SilentWithComplete
            };

            await cmd.ExecuteAsync();
            ct.ThrowIfCancellationRequested();

            if (cmd.NotModified)
                return;

            if (cmd.IsSucceed && !string.IsNullOrWhiteSpace(cmd.Json))
            {
                _rawByName[entry.Name] = cmd.Json;
                _etagByName[entry.Name] = string.IsNullOrEmpty(cmd.ETag) ? entry.Etag : cmd.ETag;
                WriteSnapshotFile(entry.Name, cmd.Json);
            }
            else
            {
                Debug.LogWarning($"{LogPrefix} fetch '{entry.Name}' failed ({cmd.Error}) — keeping local copy.");
            }
        }

        private void LoadSnapshot()
        {
            try
            {
                if (!Directory.Exists(_snapshotDir))
                    return;

                // ETag-метаданные из снапшотного манифеста.
                var manifestPath = Path.Combine(_snapshotDir, ManifestFileName);
                if (File.Exists(manifestPath))
                {
                    var entries = JsonConvert.DeserializeObject<List<ManifestEntry>>(File.ReadAllText(manifestPath));
                    if (entries != null)
                        foreach (var e in entries)
                            if (!string.IsNullOrEmpty(e.Name))
                                _etagByName[e.Name] = e.Etag;
                }

                foreach (var path in Directory.GetFiles(_snapshotDir, "*.json", SearchOption.TopDirectoryOnly))
                {
                    var name = Path.GetFileNameWithoutExtension(path);
                    if (string.Equals(name, "manifest", StringComparison.OrdinalIgnoreCase))
                        continue;
                    _rawByName[name] = File.ReadAllText(path);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} failed to read snapshot: {ex.Message}");
            }
        }

        private void WriteSnapshotFile(string name, string json)
        {
            try
            {
                Directory.CreateDirectory(_snapshotDir);
                var tmp = Path.Combine(_snapshotDir, name + ".json.tmp");
                var dst = Path.Combine(_snapshotDir, name + ".json");
                File.WriteAllText(tmp, json);
                if (File.Exists(dst)) File.Delete(dst);
                File.Move(tmp, dst);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} failed to persist '{name}': {ex.Message}");
            }
        }

        private void PersistManifest(List<ManifestEntry> manifest)
        {
            try
            {
                Directory.CreateDirectory(_snapshotDir);
                // Сохраняем актуальные ETag (с учётом применённых апдейтов).
                foreach (var e in manifest)
                    if (!string.IsNullOrEmpty(e.Name) && _etagByName.TryGetValue(e.Name, out var et))
                        e.Etag = et;
                File.WriteAllText(Path.Combine(_snapshotDir, ManifestFileName), JsonConvert.SerializeObject(manifest));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} failed to persist manifest: {ex.Message}");
            }
        }

        private static string Combine(string baseUrl, string path)
            => $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";

        [Serializable]
        private sealed class ManifestEntry
        {
            [JsonProperty("name")] public string Name;
            [JsonProperty("version")] public long Version;
            [JsonProperty("etag")] public string Etag;
        }
    }
}
