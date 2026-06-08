using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Save.Model;
using Save.Storage;
using UnityEngine;

namespace Save
{
    public sealed class SaveService : ISaveService
    {
        private const int CurrentSchemaVersion = 1;
        private const string HashSalt = "bookstore_save_v1_salt";
        private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(600);
        private static readonly TimeSpan SaveRateLimit = TimeSpan.FromMilliseconds(500);

        private readonly ISaveStorage _storage;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly List<ISaveHook> _hooks = new();
        private CancellationTokenSource _debounceCts = new();

        private SaveData _data;
        private bool _isLoaded;
        private bool _isDirty;
        private bool _disposed;
        private DateTimeOffset _lastSaveUtc = DateTimeOffset.MinValue;

        public SaveService(ISaveStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public void RegisterHook(ISaveHook hook)
        {
            if (hook != null) _hooks.Add(hook);
        }

        public async UniTask LoadAsync(CancellationToken ct)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();

            await _semaphore.WaitAsync(ct);
            try
            {
                if (_isLoaded) return;

                var json = await _storage.LoadAsync(ct);
                _data = DeserializeOrDefault(json, out var usedDefault, out var reason);
                Debug.Log(
                    $"[SaveService] LoadAsync complete. " +
                    $"UsedDefault={usedDefault}, Reason={reason}, " +
                    $"Modules={_data.Modules.Count}, Revision={_data.Meta.Revision}");
                _isLoaded = true;
            }
            finally
            {
                _semaphore.Release();
            }

            foreach (var hook in _hooks)
                await hook.AfterLoadAsync(ct);
        }

        public async UniTask SaveAsync(CancellationToken ct)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();
            await EnsureLoadedAsync(ct);

            // Fix 1: rate limit выполняется ДО захвата семафора.
            // В Research этот Delay был внутри try-блока после WaitAsync,
            // что блокировало UpdateModuleAsync на 500мс.
            var elapsed = DateTimeOffset.UtcNow - _lastSaveUtc;
            if (elapsed < SaveRateLimit)
                await UniTask.Delay(SaveRateLimit - elapsed, cancellationToken: ct);

            // Fix 3: async hooks выполняются ДО захвата семафора,
            // чтобы они могли вызывать UpdateModuleAsync (flush state).
            foreach (var hook in _hooks)
                await hook.BeforeSaveAsync(ct);

            string jsonToSave = null;
            await _semaphore.WaitAsync(ct);
            try
            {
                if (!_isDirty) return;

                _data.Meta.SchemaVersion = CurrentSchemaVersion;
                _data.Meta.TimestampUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _data.Meta.Revision = Math.Max(0, _data.Meta.Revision) + 1;
                _data.Meta.Hash = string.Empty;
                _data.Meta.Hash = ComputeHash(_data);
                jsonToSave = JsonConvert.SerializeObject(_data, Formatting.None);
                LogPayloadBreakdown(_data, jsonToSave);
            }
            finally
            {
                _semaphore.Release();
            }

            if (jsonToSave == null) return;

            await _storage.SaveAsync(jsonToSave, ct);

            await _semaphore.WaitAsync(ct);
            try
            {
                _isDirty = false;
                _lastSaveUtc = DateTimeOffset.UtcNow;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async UniTask<T> GetModuleAsync<T>(string moduleKey, CancellationToken ct) where T : class
        {
            ThrowIfDisposed();
            await EnsureLoadedAsync(ct);
            await _semaphore.WaitAsync(ct);
            try
            {
                ct.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(moduleKey))
                    throw new ArgumentNullException(nameof(moduleKey));

                if (!_data.Modules.TryGetValue(moduleKey, out var payload))
                    return null;

                return JsonConvert.DeserializeObject<T>(payload.Json);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async UniTask UpdateModuleAsync<T>(
            string moduleKey, T value, int schemaVersion, CancellationToken ct)
        {
            ThrowIfDisposed();
            await EnsureLoadedAsync(ct);
            await _semaphore.WaitAsync(ct);
            try
            {
                ct.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(moduleKey))
                    throw new ArgumentNullException(nameof(moduleKey));

                var json = JsonConvert.SerializeObject(value, Formatting.None);
                _data.Modules[moduleKey] = new ModulePayload { Version = schemaVersion, Json = json };
                _isDirty = true;
            }
            finally
            {
                _semaphore.Release();
            }

            MarkDirty();
        }

        public void MarkDirty()
        {
            if (_disposed) return;
            _debounceCts.Cancel();
            _debounceCts.Dispose();
            _debounceCts = new CancellationTokenSource();
            DebouncedSaveAsync(_debounceCts.Token).Forget();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _debounceCts.Cancel();
            _debounceCts.Dispose();
            _semaphore.Dispose();
        }

        private async UniTask EnsureLoadedAsync(CancellationToken ct)
        {
            if (_isLoaded) return;
            await LoadAsync(ct);
        }

        private async UniTaskVoid DebouncedSaveAsync(CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(DebounceDelay, cancellationToken: ct);
                await SaveAsync(ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveService] Debounced save failed: {ex}");
            }
        }

        private static SaveData DeserializeOrDefault(string json, out bool usedDefault, out string reason)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                usedDefault = true;
                reason = "empty-payload";
                return CreateDefault();
            }

            try
            {
                var parsed = JsonConvert.DeserializeObject<SaveData>(json);
                if (parsed != null)
                {
                    parsed.Meta ??= new MetaData();
                    parsed.Modules ??= new Dictionary<string, ModulePayload>();
                    usedDefault = false;
                    reason = "ok";
                    return parsed;
                }

                usedDefault = true;
                reason = "deserialized-null";
                return CreateDefault();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveService] Save corrupted, falling back to defaults. {ex.Message}");
                usedDefault = true;
                reason = $"exception:{ex.GetType().Name}";
                return CreateDefault();
            }
        }

        private static SaveData CreateDefault() => new()
        {
            Meta = new MetaData { SchemaVersion = CurrentSchemaVersion },
            Modules = new Dictionary<string, ModulePayload>()
        };

        private static string ComputeHash(SaveData data)
        {
            var prev = data.Meta.Hash;
            data.Meta.Hash = string.Empty;
            var json = JsonConvert.SerializeObject(data, Formatting.None);
            data.Meta.Hash = prev;

            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(json + HashSalt);
            var hash = sha.ComputeHash(bytes);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static void LogPayloadBreakdown(SaveData data, string fullJson)
        {
            const int limitBytes = 30720;
            var total = Encoding.UTF8.GetByteCount(fullJson ?? "");
            var over = total > limitBytes;
            var msg = $"[SaveService] Payload total={total}B (limit={limitBytes}B, {(over ? "OVER" : "ok")}), modules={data.Modules.Count}";
            if (over) Debug.LogWarning(msg);
            else Debug.Log(msg);

            if (data.Modules.Count <= 0) return;
            foreach (var kvp in data.Modules)
            {
                var size = kvp.Value?.Json == null ? 0 : Encoding.UTF8.GetByteCount(kvp.Value.Json);
                Debug.Log($"[SaveService]   module='{kvp.Key}' v{kvp.Value?.Version} = {size}B");
            }
        }

        private static string TruncateForLog(string value, int maxLength = 320)
        {
            if (string.IsNullOrWhiteSpace(value)) return "<empty>";
            var normalized = value.Replace("\r", "\\r").Replace("\n", "\\n");
            return normalized.Length <= maxLength ? normalized : normalized.Substring(0, maxLength) + "...";
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SaveService));
        }
    }
}
