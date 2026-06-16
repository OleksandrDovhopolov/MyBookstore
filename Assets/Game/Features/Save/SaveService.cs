using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        private readonly ISaveStorage _localStorageOverride; // used for ForceLocalOnly mode
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly List<ISaveHook> _hooks = new();
        private CancellationTokenSource _debounceCts = new();

        private SaveData _data;
        private bool _isLoaded;
        private bool _isDirty;
        private bool _disposed;
        private DateTimeOffset _lastSaveUtc = DateTimeOffset.MinValue;
        private int _autosaveBlockCount;
        private bool _hasPendingAutosave;

        // localStorageOverride: if provided, used for SaveMode.ForceLocalOnly (skips server push).
        // Typically LocalDiskStorage when primary storage is HttpSaveStorage.
        public SaveService(ISaveStorage storage, ISaveStorage localStorageOverride = null)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _localStorageOverride = localStorageOverride;
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

        public async UniTask SaveAsync(CancellationToken ct, SaveMode mode = SaveMode.Regular)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();
            await EnsureLoadedAsync(ct);

            var elapsed = DateTimeOffset.UtcNow - _lastSaveUtc;
            if (mode != SaveMode.ForceWithSync && elapsed < SaveRateLimit)
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

            var targetStorage = mode == SaveMode.ForceLocalOnly ? _localStorageOverride ?? _storage : _storage;
            await targetStorage.SaveAsync(jsonToSave, ct);

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

                if (!_data.Modules.TryGetValue(moduleKey, out var payload) || payload.Json == null)
                    return null;

                return payload.Json.ToObject<T>();
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

                // FromObject keeps the payload as a structured JToken — the outer SaveData
                // serializer then emits it inline instead of an escaped string.
                var token = value == null ? null : JToken.FromObject(value);
                _data.Modules[moduleKey] = new ModulePayload { Version = schemaVersion, Json = token };
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

            if (_autosaveBlockCount > 0)
            {
                _hasPendingAutosave = true;
                return;
            }

            DebouncedSaveAsync(_debounceCts.Token).Forget();
        }

        public IDisposable BlockAutosave()
        {
            ThrowIfDisposed();
            System.Threading.Interlocked.Increment(ref _autosaveBlockCount);
            return new AutosaveLease(this);
        }

        private void ReleaseAutosaveLease()
        {
            if (System.Threading.Interlocked.Decrement(ref _autosaveBlockCount) == 0 && _hasPendingAutosave)
            {
                _hasPendingAutosave = false;
                MarkDirty();
            }
        }

        private sealed class AutosaveLease : IDisposable
        {
            private SaveService _owner;

            public AutosaveLease(SaveService owner) => _owner = owner;

            public void Dispose()
            {
                var o = System.Threading.Interlocked.Exchange(ref _owner, null);
                o?.ReleaseAutosaveLease();
            }
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
            const int moduleLimit = 5120;
            foreach (var kvp in data.Modules)
            {
                var size = kvp.Value?.Json == null
                    ? 0
                    : Encoding.UTF8.GetByteCount(kvp.Value.Json.ToString(Formatting.None));
                var moduleMsg = $"[SaveService]   module='{kvp.Key}' v{kvp.Value?.Version} = {size}B";
                if (size > moduleLimit) Debug.LogWarning(moduleMsg + " (OVER 5KB)");
                else Debug.Log(moduleMsg);
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
