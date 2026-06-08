using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands;
using Game.Http;
using Save.Config;
using Save.Identity;
using Save.Storage.Commands;
using UnityEngine;

namespace Save.Storage
{
    // Write-through cache: SaveAsync writes locally first, then pushes to the server.
    // On network errors no progress is lost — the next save will retry the push.
    // Transport goes through Game.Http command infrastructure (retries, connection check,
    // unified logging, error reporting) instead of raw UnityWebRequest.
    public sealed class HttpSaveStorage : ISaveStorage
    {
        private const string LogPrefix = "[HttpSaveStorage]";

        private readonly ISaveBackendConfig _config;
        private readonly ISaveStorage _localCache;
        private readonly IPlayerIdentityProvider _identity;
        private readonly IConnectionService _connectionService;
        private readonly ICommandLogger _logger;
        private readonly ICommandErrorReporter _errorReporter;

        public HttpSaveStorage(
            ISaveBackendConfig config,
            ISaveStorage localCache,
            IPlayerIdentityProvider identity,
            IConnectionService connectionService,
            ICommandLogger logger,
            ICommandErrorReporter errorReporter)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _localCache = localCache ?? throw new ArgumentNullException(nameof(localCache));
            _identity = identity ?? throw new ArgumentNullException(nameof(identity));
            _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _errorReporter = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));
        }

        public async UniTask SaveAsync(string data, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            // 1. Local atomic write — progress guaranteed even if the server push fails.
            await _localCache.SaveAsync(data, ct);

            // 2. Push via command — retries, timeouts, connection checks handled by Game.Http.
            var cmd = new PostSaveGlobalCommand(
                _connectionService, _logger, _errorReporter,
                BuildUrl(), _identity.GetPlayerId(), data);

            await cmd.ExecuteAsync();

            if (cmd.IsSucceed)
                Debug.Log($"{LogPrefix} SaveAsync: server push succeeded.");
            else
                Debug.LogWarning($"{LogPrefix} SaveAsync: server push failed ({cmd.Error}). Data safe locally.");
        }

        public async UniTask<string> LoadAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var cmd = new GetSaveGlobalCommand(_connectionService, _logger, _errorReporter, BuildUrl());
            await cmd.ExecuteAsync();

            // 404 → new user, no server save yet.
            if (cmd.Error == ConnectionCommandsErrors.NotFoundError)
            {
                Debug.Log($"{LogPrefix} LoadAsync: no remote save (new user).");
                return null;
            }

            if (cmd.IsSucceed)
            {
                var normalized = cmd.NormalizedData;
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    await _localCache.SaveAsync(normalized, ct);
                    Debug.Log($"{LogPrefix} LoadAsync: loaded from server, local cache updated.");
                    return normalized;
                }

                Debug.LogWarning($"{LogPrefix} LoadAsync: server returned empty payload, falling back to local cache.");
            }
            else
            {
                Debug.LogWarning($"{LogPrefix} LoadAsync: server failed ({cmd.Error}), falling back to local cache.");
            }

            var cached = await _localCache.LoadAsync(ct);
            Debug.Log($"{LogPrefix} LoadAsync: using local cache, data={(cached == null ? "null" : "present")}.");
            return cached;
        }

        // Used by SaveSyncBootstrap to compare revisions without write-through.
        public async UniTask<string> PeekServerAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var cmd = new GetSaveGlobalCommand(_connectionService, _logger, _errorReporter, BuildUrl());
            await cmd.ExecuteAsync();

            if (cmd.Error == ConnectionCommandsErrors.NotFoundError) return null;
            if (cmd.IsSucceed) return cmd.NormalizedData;

            throw new InvalidOperationException($"{LogPrefix} PeekServerAsync failed: {cmd.Error}");
        }

        public async UniTask DeleteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _localCache.DeleteAsync(ct);

            // DELETE is not migrated to the command infrastructure yet because Game.Http
            // currently only supports GET/POST (see UnityWebRequestAdapter.BuildRequest).
            // Server-side deletion is not part of the normal save flow — skip with a warning.
            Debug.LogWarning($"{LogPrefix} DeleteAsync: server-side delete is not supported via command infrastructure; only local cache cleared.");
        }

        public UniTask<long> GetLastModifiedTimestampAsync(CancellationToken ct)
        {
            // HEAD is not part of HTTPMethods (Game.Http supports GET/POST only).
            // This API is consumed by SaveSyncBootstrap, which is deferred to a separate task.
            ct.ThrowIfCancellationRequested();
            return UniTask.FromResult(0L);
        }

        private string BuildUrl()
        {
            var baseUrl = _config.BaseUrl.TrimEnd('/');
            var path = _config.SavePath.TrimStart('/');
            var playerId = Uri.EscapeDataString(_identity.GetPlayerId());
            var full = $"{baseUrl}/{path}";
            var sep = full.Contains("?") ? "&" : "?";
            return $"{full}{sep}playerId={playerId}";
        }
    }
}
