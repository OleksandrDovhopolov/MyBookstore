using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Save.Model;
using Save.Storage;
using UnityEngine;

namespace Save.Sync
{
    public sealed class SaveSyncBootstrap
    {
        private readonly LocalDiskStorage _localStorage;
        private readonly HttpSaveStorage _httpStorage;

        public SaveSyncBootstrap(LocalDiskStorage localStorage, HttpSaveStorage httpStorage)
        {
            _localStorage = localStorage ?? throw new ArgumentNullException(nameof(localStorage));
            _httpStorage = httpStorage ?? throw new ArgumentNullException(nameof(httpStorage));
        }

        public async UniTask SyncOnStartupAsync(CancellationToken ct)
        {
            var localJson = await _localStorage.LoadAsync(ct);
            var localMeta = TryParseMeta(localJson);
            var localHasProgress = HasMeaningfulProgress(localJson);

            string serverJson;
            try
            {
                serverJson = await _httpStorage.PeekServerAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Debug.LogWarning($"[SaveSyncBootstrap] Server unreachable, skipping sync. {ex.Message}");
                if (localHasProgress)
                    _httpStorage.UseLocalCacheForNextLoad();
                return;
            }

            var serverMeta = TryParseMeta(serverJson);

            if (ShouldLocalWinOverServer(localJson, serverJson))
            {
                await _httpStorage.SaveAsync(localJson, ct);
                _httpStorage.UseLocalCacheForNextLoad();
                Debug.Log("[SaveSyncBootstrap] Local save wins over missing/default server save, attempted server push.");
                return;
            }

            if (localMeta == null)
            {
                Debug.Log("[SaveSyncBootstrap] No local save, server data will be loaded normally.");
                return;
            }

            if (serverMeta == null)
            {
                Debug.Log("[SaveSyncBootstrap] No comparable server save, keeping local.");
            }
            else if (serverMeta.Revision > localMeta.Revision)
            {
                await _localStorage.SaveAsync(serverJson, ct);
                Debug.Log($"[SaveSyncBootstrap] Server newer (rev {serverMeta.Revision} > {localMeta.Revision}), local updated.");
            }
            else if (localMeta.Revision > serverMeta.Revision)
            {
                await _httpStorage.SaveAsync(localJson, ct);
                _httpStorage.UseLocalCacheForNextLoad();
                Debug.Log($"[SaveSyncBootstrap] Local newer (rev {localMeta.Revision} > {serverMeta.Revision}), attempted server push.");
            }
            else
            {
                Debug.Log($"[SaveSyncBootstrap] Revisions equal ({localMeta.Revision}), no sync needed.");
            }
        }

        private static MetaData TryParseMeta(string json) => TryParseSave(json)?.Meta;

        internal static bool ShouldLocalWinOverServer(string localJson, string serverJson)
        {
            var localMeta = TryParseMeta(localJson);
            var serverMeta = TryParseMeta(serverJson);
            return localMeta != null &&
                   (serverMeta == null || (HasMeaningfulProgress(localJson) && LooksLikeMaterializedDefault(serverJson)));
        }

        private static bool HasMeaningfulProgress(string json)
        {
            var save = TryParseSave(json);
            return save?.Modules is { Count: > 0 } || (save?.Meta?.Revision ?? 0) > 1;
        }

        private static bool LooksLikeMaterializedDefault(string json)
        {
            var save = TryParseSave(json);
            if (save == null)
                return false;

            var revision = save.Meta?.Revision ?? 0;
            return (save.Modules == null || save.Modules.Count == 0) && revision <= 1;
        }

        private static SaveData TryParseSave(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonConvert.DeserializeObject<SaveData>(json);
            }
            catch
            {
                return null;
            }
        }
    }
}
