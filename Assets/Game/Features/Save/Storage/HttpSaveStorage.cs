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
    public sealed class HttpSaveStorage : ISaveStorage
    {
        private const string LogPrefix = "[HttpSaveStorage]";

        private readonly ISaveBackendConfig _config;
        private readonly LocalDiskStorage _localCache;
        private readonly IPlayerIdentityProvider _identity;
        private readonly IConnectionService _connectionService;
        private readonly ICommandLogger _logger;
        private readonly ICommandErrorReporter _errorReporter;
        private bool _useLocalCacheForNextLoad;

        public HttpSaveStorage(
            ISaveBackendConfig config,
            LocalDiskStorage localCache,
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

            await _localCache.SaveAsync(data, ct);

            var cmd = new PostSaveGlobalCommand(
                _connectionService,
                _logger,
                _errorReporter,
                BuildSaveUrl(),
                _identity.GetPlayerId(),
                data);
            cmd.ConnectionCheckBehaviour = ConnectionCheckBehaviour.SilentWithComplete;

            await cmd.ExecuteAsync();

            if (cmd.IsSucceed)
                Debug.Log($"{LogPrefix} SaveAsync: server push succeeded.");
            else
                Debug.LogWarning($"{LogPrefix} SaveAsync: server push failed ({cmd.Error}). Data safe locally.");
        }

        public async UniTask<string> LoadAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (_useLocalCacheForNextLoad)
            {
                _useLocalCacheForNextLoad = false;
                var local = await _localCache.LoadAsync(ct);
                Debug.Log($"{LogPrefix} LoadAsync: using local cache after startup sync.");
                return local;
            }

            var cmd = new GetSaveGlobalCommand(_connectionService, _logger, _errorReporter, BuildLoadUrl());
            cmd.ConnectionCheckBehaviour = ConnectionCheckBehaviour.SilentWithComplete;
            await cmd.ExecuteAsync();

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

        public async UniTask<string> PeekServerAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var cmd = new GetSaveGlobalCommand(_connectionService, _logger, _errorReporter, BuildLoadUrl());
            cmd.ConnectionCheckBehaviour = ConnectionCheckBehaviour.SilentWithComplete;
            await cmd.ExecuteAsync();

            if (cmd.Error == ConnectionCommandsErrors.NotFoundError) return null;
            if (cmd.IsSucceed) return cmd.NormalizedData;

            throw new InvalidOperationException($"{LogPrefix} PeekServerAsync failed: {cmd.Error}");
        }

        public void UseLocalCacheForNextLoad()
        {
            _useLocalCacheForNextLoad = true;
        }

        public async UniTask DeleteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _localCache.DeleteAsync(ct);
            Debug.LogWarning($"{LogPrefix} DeleteAsync: server-side delete is not supported; only local cache cleared.");
        }

        public UniTask<long> GetLastModifiedTimestampAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return UniTask.FromResult(0L);
        }

        private string BuildSaveUrl()
        {
            return BuildBaseSaveUrl();
        }

        private string BuildLoadUrl()
        {
            var full = BuildBaseSaveUrl();
            var playerId = Uri.EscapeDataString(_identity.GetPlayerId());
            var sep = full.Contains("?") ? "&" : "?";
            return $"{full}{sep}playerId={playerId}";
        }

        private string BuildBaseSaveUrl()
        {
            var baseUrl = _config.BaseUrl.TrimEnd('/');
            var path = _config.SavePath.TrimStart('/');
            return $"{baseUrl}/{path}";
        }
    }
}
