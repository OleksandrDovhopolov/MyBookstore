using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Save.Config;
using Save.Identity;
using UnityEngine;
using UnityEngine.Networking;

namespace Save.Storage
{
    // Write-through кэш: SaveAsync сначала пишет локально, потом пушит на сервер.
    // При сетевой ошибке прогресс не теряется — на следующем сейве push повторится.
    // URL берётся из ISaveBackendConfig (не захардкожен).
    public sealed class HttpSaveStorage : ISaveStorage
    {
        private const string LogPrefix = "[HttpSaveStorage]";

        private readonly ISaveBackendConfig _config;
        private readonly ISaveStorage _localCache;
        private readonly IPlayerIdentityProvider _identity;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public HttpSaveStorage(
            ISaveBackendConfig config,
            ISaveStorage localCache,
            IPlayerIdentityProvider identity)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _localCache = localCache ?? throw new ArgumentNullException(nameof(localCache));
            _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        }

        public async UniTask SaveAsync(string data, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                // 1. Записать локально атомарно — данные гарантированно сохранены.
                await _localCache.SaveAsync(data, ct);

                // 2. Push на сервер. Ошибка сети не теряет данные — они уже на диске.
                try
                {
                    await PushToServerAsync(data, ct);
                    Debug.Log($"{LogPrefix} SaveAsync: server push succeeded.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Debug.LogWarning($"{LogPrefix} SaveAsync: server push failed, data safe locally. {ex.Message}");
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async UniTask<string> LoadAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                // Сервер — приоритетный источник (свежее).
                try
                {
                    var fromServer = await PullFromServerAsync(ct);
                    if (fromServer != null)
                    {
                        await _localCache.SaveAsync(fromServer, ct);
                        Debug.Log($"{LogPrefix} LoadAsync: loaded from server, local cache updated.");
                        return fromServer;
                    }

                    Debug.Log($"{LogPrefix} LoadAsync: no remote save (new user).");
                    return null;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Debug.LogWarning($"{LogPrefix} LoadAsync: server failed, falling back to local cache. {ex.Message}");
                }

                var cached = await _localCache.LoadAsync(ct);
                Debug.Log($"{LogPrefix} LoadAsync: using local cache, data={(cached == null ? "null" : "present")}.");
                return cached;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async UniTask DeleteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                await _localCache.DeleteAsync(ct);
                try
                {
                    await DeleteFromServerAsync(ct);
                    Debug.Log($"{LogPrefix} DeleteAsync: server delete succeeded.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Debug.LogWarning($"{LogPrefix} DeleteAsync: server delete failed. {ex.Message}");
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async UniTask<long> GetLastModifiedTimestampAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var url = BuildUrl();
            await _semaphore.WaitAsync(ct);
            try
            {
                using var request = UnityWebRequest.Head(url);
                ApplyHeaders(request);
                try
                {
                    await request.SendWebRequest().ToUniTask(cancellationToken: ct);
                }
                catch (UnityWebRequestException ex) when (ex.ResponseCode == 404)
                {
                    return 0;
                }

                if (request.responseCode == 404) return 0;
                ThrowIfFailed(request, nameof(GetLastModifiedTimestampAsync));

                var unixHeader = request.GetResponseHeader("X-Last-Modified-Unix");
                if (long.TryParse(unixHeader, out var unixSeconds))
                    return unixSeconds;

                var lastModifiedHeader = request.GetResponseHeader("Last-Modified");
                if (DateTimeOffset.TryParse(lastModifiedHeader, out var parsed))
                    return parsed.ToUnixTimeSeconds();

                return 0;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        // Used by SaveSyncBootstrap to compare revisions without write-through.
        public async UniTask<string> PeekServerAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return await PullFromServerAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException($"{LogPrefix} PeekServerAsync failed. {ex.Message}", ex);
            }
        }

        private async UniTask PushToServerAsync(string data, CancellationToken ct)
        {
            var url = BuildUrl();
            // Server contract: data is a JSON-escaped STRING, not an embedded object.
            //   { "playerId": "...", "data": "{\"Meta\":...}" }
            var payload = JsonConvert.SerializeObject(new { playerId = _identity.GetPlayerId(), data });
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            ApplyHeaders(request);
            await request.SendWebRequest().ToUniTask(cancellationToken: ct);
            ThrowIfFailed(request, nameof(PushToServerAsync));
        }

        private async UniTask<string> PullFromServerAsync(CancellationToken ct)
        {
            var url = BuildUrl();
            using var request = UnityWebRequest.Get(url);
            request.downloadHandler = new DownloadHandlerBuffer();
            ApplyHeaders(request);
            try
            {
                await request.SendWebRequest().ToUniTask(cancellationToken: ct);
            }
            catch (UnityWebRequestException ex) when (ex.ResponseCode == 404)
            {
                return null;
            }

            if (request.responseCode == 404) return null;
            ThrowIfFailed(request, nameof(PullFromServerAsync));

            var raw = request.downloadHandler?.text;
            var normalized = SaveGlobalPayloadParser.ExtractDataForStorage(raw, out var mode);
            Debug.Log($"{LogPrefix} PullFromServerAsync: mode={mode}, rawLen={raw?.Length ?? 0}");
            return normalized;
        }

        private async UniTask DeleteFromServerAsync(CancellationToken ct)
        {
            var url = BuildUrl();
            using var request = UnityWebRequest.Delete(url);
            request.downloadHandler = new DownloadHandlerBuffer();
            ApplyHeaders(request);
            try
            {
                await request.SendWebRequest().ToUniTask(cancellationToken: ct);
            }
            catch (UnityWebRequestException ex) when (ex.ResponseCode == 404)
            {
                return;
            }

            if (request.responseCode == 404) return;
            ThrowIfFailed(request, nameof(DeleteFromServerAsync));
        }

        private string BuildUrl()
        {
            var base_ = _config.BaseUrl.TrimEnd('/');
            var path = _config.SavePath.TrimStart('/');
            var playerId = UnityWebRequest.EscapeURL(_identity.GetPlayerId());
            var full = $"{base_}/{path}";
            var sep = full.Contains("?") ? "&" : "?";
            return $"{full}{sep}playerId={playerId}";
        }

        private void ApplyHeaders(UnityWebRequest request)
        {
            // TODO: добавить Authorization header через ISaveBackendConfig или IAuthTokenProvider
        }

        private static void ThrowIfFailed(UnityWebRequest request, string op)
        {
            if (request.result is UnityWebRequest.Result.ConnectionError
                or UnityWebRequest.Result.ProtocolError)
            {
                throw new InvalidOperationException(
                    $"{LogPrefix} {op} failed. Status={(int)request.responseCode}, Error={request.error}");
            }
        }
    }
}
