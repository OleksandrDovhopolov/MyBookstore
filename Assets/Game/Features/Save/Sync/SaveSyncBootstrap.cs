using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Save.Model;
using Save.Storage;
using UnityEngine;

namespace Save.Sync
{
    // Runs once at startup (before SaveService.LoadAsync) to resolve local vs server conflicts.
    // Algorithm: compare MetaData.Revision → last-write-wins.
    //   server newer  → overwrite local disk with server data
    //   local newer   → push local to server
    //   equal / error → do nothing (SaveService.LoadAsync proceeds normally)
    public sealed class SaveSyncBootstrap
    {
        private readonly ISaveStorage _localStorage;
        private readonly HttpSaveStorage _httpStorage;

        public SaveSyncBootstrap(ISaveStorage localStorage, HttpSaveStorage httpStorage)
        {
            _localStorage = localStorage ?? throw new ArgumentNullException(nameof(localStorage));
            _httpStorage = httpStorage ?? throw new ArgumentNullException(nameof(httpStorage));
        }

        public async UniTask SyncOnStartupAsync(CancellationToken ct)
        {
            var localJson = await _localStorage.LoadAsync(ct);
            var localMeta = TryParseMeta(localJson);

            string serverJson;
            try
            {
                serverJson = await _httpStorage.PeekServerAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Debug.LogWarning($"[SaveSyncBootstrap] Server unreachable, skipping sync. {ex.Message}");
                return;
            }

            var serverMeta = TryParseMeta(serverJson);

            if (serverMeta == null && localMeta != null)
            {
                // First launch on this server (e.g. account restored) → push local
                await _httpStorage.SaveAsync(localJson, ct);
                Debug.Log("[SaveSyncBootstrap] No server save found, pushed local.");
                return;
            }

            if (localMeta == null)
            {
                // No local data — SaveService.LoadAsync will pull from server via HttpSaveStorage
                Debug.Log("[SaveSyncBootstrap] No local save, server data will be loaded normally.");
                return;
            }

            if (serverMeta.Revision > localMeta.Revision)
            {
                await _localStorage.SaveAsync(serverJson, ct);
                Debug.Log($"[SaveSyncBootstrap] Server newer (rev {serverMeta.Revision} > {localMeta.Revision}), local updated.");
            }
            else if (localMeta.Revision > serverMeta.Revision)
            {
                await _httpStorage.SaveAsync(localJson, ct);
                Debug.Log($"[SaveSyncBootstrap] Local newer (rev {localMeta.Revision} > {serverMeta.Revision}), pushed to server.");
            }
            else
            {
                Debug.Log($"[SaveSyncBootstrap] Revisions equal ({localMeta.Revision}), no sync needed.");
            }
        }

        private static MetaData TryParseMeta(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonConvert.DeserializeObject<SaveData>(json)?.Meta;
            }
            catch
            {
                return null;
            }
        }
    }
}
