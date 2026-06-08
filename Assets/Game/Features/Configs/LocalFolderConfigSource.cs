using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Configs
{
    /// <summary>
    /// Базовый источник для Editor/локальной разработки: читает все *.json из папки.
    /// По умолчанию — Assets/Configs (Application.dataPath/Configs) в Editor.
    /// Правишь JSON → жмёшь Play → видишь изменения без раундтрипа на сервер.
    /// </summary>
    public sealed class LocalFolderConfigSource : IConfigSource
    {
        private const string LogPrefix = "[LocalFolderConfigSource]";

        private readonly string _directory;
        private readonly Dictionary<string, string> _rawByName = new(StringComparer.OrdinalIgnoreCase);

        public LocalFolderConfigSource(string directory = null)
        {
            _directory = string.IsNullOrEmpty(directory)
                ? Path.Combine(Application.dataPath, "Configs")
                : directory;
        }

        public async UniTask WarmupAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _rawByName.Clear();

            if (!Directory.Exists(_directory))
            {
                Debug.LogWarning($"{LogPrefix} Config directory not found: {_directory}");
                return;
            }

            var files = Directory.GetFiles(_directory, "*.json", SearchOption.TopDirectoryOnly);
            foreach (var path in files)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var name = Path.GetFileNameWithoutExtension(path);
                    var text = await File.ReadAllTextAsync(path, ct);
                    _rawByName[name] = text;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Debug.LogError($"{LogPrefix} Failed to read {path}: {ex.Message}");
                }
            }

            Debug.Log($"{LogPrefix} Loaded {_rawByName.Count} config file(s) from {_directory}.");
        }

        public string GetRaw(string fileName)
            => _rawByName.TryGetValue(fileName, out var raw) ? raw : null;
    }
}
