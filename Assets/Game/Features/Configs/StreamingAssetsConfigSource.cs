using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

[assembly: InternalsVisibleTo("Configs.Tests.Editor")]

namespace Game.Configs
{
    /// <summary>
    /// Bundled-defaults для плеер-сборки: читает StreamingAssets/Configs/ через UnityWebRequest
    /// (на Android прямой File.IO не работает — путь внутри APK jar).
    /// Список файлов берётся из manifest.json — Directory.GetFiles по StreamingAssets недоступен.
    /// </summary>
    public sealed class StreamingAssetsConfigSource : IConfigSource
    {
        private const string LogPrefix = "[StreamingAssetsConfigSource]";
        private const string ManifestFileName = "manifest.json";
        private const string DefaultSubfolder = "Configs/";

        private readonly string _baseUrl;
        private readonly Dictionary<string, string> _rawByName = new(StringComparer.OrdinalIgnoreCase);

        public StreamingAssetsConfigSource()
            : this(BuildDefaultBaseUrl()) { }

        internal StreamingAssetsConfigSource(string baseUrl)
        {
            if (string.IsNullOrEmpty(baseUrl))
                throw new ArgumentException("baseUrl must be non-empty.", nameof(baseUrl));
            _baseUrl = baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/";
        }

        public async UniTask WarmupAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _rawByName.Clear();

            var manifestUrl = _baseUrl + ManifestFileName;
            string manifestJson;
            try
            {
                manifestJson = await DownloadTextAsync(manifestUrl, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Debug.LogError($"{LogPrefix} Failed to load manifest at {manifestUrl}: {ex.Message}");
                return;
            }

            string[] entries;
            try
            {
                entries = JsonConvert.DeserializeObject<string[]>(manifestJson);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogPrefix} Failed to parse manifest at {manifestUrl}: {ex.Message}");
                return;
            }

            if (entries == null || entries.Length == 0)
            {
                Debug.LogWarning($"{LogPrefix} Manifest at {manifestUrl} is empty.");
                return;
            }

            var tasks = new List<UniTask<(string key, string text)>>(entries.Length);
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (string.IsNullOrWhiteSpace(entry)) continue;
                tasks.Add(LoadEntryAsync(entry, ct));
            }

            var results = await UniTask.WhenAll(tasks);
            for (var i = 0; i < results.Length; i++)
            {
                var (key, text) = results[i];
                if (key != null && text != null)
                    _rawByName[key] = text;
            }

            Debug.Log($"{LogPrefix} Loaded {_rawByName.Count} config file(s) from {_baseUrl}.");
        }

        public string GetRaw(string fileName)
            => _rawByName.TryGetValue(fileName, out var raw) ? raw : null;

        private async UniTask<(string key, string text)> LoadEntryAsync(string entry, CancellationToken ct)
        {
            var url = _baseUrl + entry;
            try
            {
                var text = await DownloadTextAsync(url, ct);
                var key = Path.GetFileNameWithoutExtension(entry);
                return (key, text);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} Failed to load {url}: {ex.Message}");
                return (null, null);
            }
        }

        private static async UniTask<string> DownloadTextAsync(string url, CancellationToken ct)
        {
            using var req = UnityWebRequest.Get(url);
            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                if (ct.IsCancellationRequested)
                {
                    req.Abort();
                    ct.ThrowIfCancellationRequested();
                }
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            if (req.result != UnityWebRequest.Result.Success)
                throw new IOException($"{req.result}: {req.error}");

            return req.downloadHandler.text;
        }

        private static string BuildDefaultBaseUrl()
        {
            var path = Path.Combine(Application.streamingAssetsPath, DefaultSubfolder).Replace('\\', '/');
            // Android already returns jar:file://...; other platforms need file:// for UnityWebRequest.
            if (path.Contains("://")) return path;
            return "file://" + path;
        }
    }
}
