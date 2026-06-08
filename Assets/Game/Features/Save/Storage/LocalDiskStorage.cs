using System;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Save.Storage
{
    // Источник: Research/LocalDiskStorage.cs — без изменений логики.
    // Exists() убран — интерфейс ISaveStorage его не требует (null из LoadAsync = нет данных).
    public sealed class LocalDiskStorage : ISaveStorage
    {
        private readonly string _filePath;
        private readonly string _backupPath;
        private readonly SemaphoreSlim _ioSemaphore = new(1, 1);

        public LocalDiskStorage(string fileName = "bookstore_save.json")
        {
            var rootPath = Application.persistentDataPath;
            _filePath = Path.Combine(rootPath, fileName);
            _backupPath = _filePath + ".bak";
        }

        public async UniTask SaveAsync(string data, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _ioSemaphore.WaitAsync(ct);
            try
            {
                ct.ThrowIfCancellationRequested();
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var tempPath = _filePath + ".tmp";
                await File.WriteAllTextAsync(tempPath, data ?? string.Empty, Encoding.UTF8, ct);

                if (File.Exists(_filePath))
                    File.Replace(tempPath, _filePath, _backupPath, ignoreMetadataErrors: true);
                else
                    File.Move(tempPath, _filePath);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Debug.LogError($"[LocalDiskStorage] Save failed: {ex}");
                throw;
            }
            finally
            {
                _ioSemaphore.Release();
            }
        }

        public async UniTask<string> LoadAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _ioSemaphore.WaitAsync(ct);
            try
            {
                ct.ThrowIfCancellationRequested();
                if (!File.Exists(_filePath))
                    return null;

                return await File.ReadAllTextAsync(_filePath, ct);
            }
            finally
            {
                _ioSemaphore.Release();
            }
        }

        public async UniTask DeleteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _ioSemaphore.WaitAsync(ct);
            try
            {
                ct.ThrowIfCancellationRequested();
                if (File.Exists(_filePath))
                    File.Delete(_filePath);
                if (File.Exists(_backupPath))
                    File.Delete(_backupPath);
            }
            finally
            {
                _ioSemaphore.Release();
            }
        }

        public async UniTask<long> GetLastModifiedTimestampAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _ioSemaphore.WaitAsync(ct);
            try
            {
                ct.ThrowIfCancellationRequested();
                if (!File.Exists(_filePath))
                    return 0;

                return new DateTimeOffset(File.GetLastWriteTimeUtc(_filePath)).ToUnixTimeSeconds();
            }
            finally
            {
                _ioSemaphore.Release();
            }
        }
    }
}
