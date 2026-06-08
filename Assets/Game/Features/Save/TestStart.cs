using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands;
using Game.Http;
using Save.Config;
using Save.Identity;
using Save.Storage;
using UnityEngine;

namespace Save
{
    // Smoke-test системы сохранений. Запускается со сцены — повесить на любой GameObject.
    // На каждом запуске:
    //   1. Загружает сейв с диска
    //   2. Читает счётчик из модуля "test_counter"
    //   3. Увеличивает на 1, обновляет timestamp
    //   4. Сохраняет с ForceWithSync (мгновенный flush)
    //   5. Делает round-trip read для верификации (in-memory deserialize)
    //   6. Выводит путь к файлу и его содержимое
    // Повторный запуск Play должен показать инкремент счётчика — это подтверждает,
    // что данные реально лежат на диске.
    [Obsolete("Test Class")]
    public class TestStart : MonoBehaviour
    {
        private const string ModuleKey = "test_counter";
        private const int SchemaVersion = 1;

        private ISaveService _save;
        private CancellationTokenSource _cts;

        private sealed class TestData
        {
            public int Counter { get; set; }
            public string LastWriteIso { get; set; }
        }

        private async void Start()
        {
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            // Standalone wiring — без VContainer, чтобы тест не зависел от настроек сцены.
            // HTTP-режим: HttpSaveStorage — primary, LocalDiskStorage — write-through кэш.
            // Сетевой стек: команда → IConnectionService → IRequestFactory (Unity adapter).
            var localCache = new LocalDiskStorage();
            var identity = new PersistentInstallPlayerIdentityProvider();
            var config = new SaveBackendConfig();
            var logger = new UnityCommandLogger(CommandLogLevel.Info);
            var errorReporter = new NoOpCommandErrorReporter();
            var requestFactory = new UnityWebRequestFactory();
            var connectionService = new ConnectionService(requestFactory);
            var http = new HttpSaveStorage(config, localCache, identity, connectionService, logger, errorReporter);
            _save = new SaveService(http, localCache);

            Debug.Log($"[TestStart] backend: {config.BaseUrl}{config.SavePath}");

            try
            {
                // 1. Load
                await _save.LoadAsync(ct);

                // 2. Read current value (null → first run)
                var data = await _save.GetModuleAsync<TestData>(ModuleKey, ct);
                var isFirstRun = data == null;
                data ??= new TestData();

                Debug.Log(
                    $"[TestStart] BEFORE: counter={data.Counter}, lastWrite={data.LastWriteIso ?? "<none>"}, " +
                    $"firstRun={isFirstRun}");

                // 3. Mutate
                data.Counter += 1;
                data.LastWriteIso = DateTime.UtcNow.ToString("O");

                // 4. Update & Save
                await _save.UpdateModuleAsync(ModuleKey, data, SchemaVersion, ct);
                await _save.SaveAsync(ct, SaveMode.ForceWithSync);

                Debug.Log($"[TestStart] SAVED: counter={data.Counter}");

                // 5. Round-trip verification (in-memory — проверяет сериализация/десериализация)
                var roundTrip = await _save.GetModuleAsync<TestData>(ModuleKey, ct);
                if (roundTrip != null && roundTrip.Counter == data.Counter)
                    Debug.Log($"[TestStart] round-trip OK: counter={roundTrip.Counter}");
                else
                    Debug.LogError(
                        $"[TestStart] round-trip FAILED: expected={data.Counter}, got={roundTrip?.Counter.ToString() ?? "<null>"}");

                // 6. Disk verification — путь и содержимое
                LogSaveFile();

                Debug.Log("[TestStart] Done. Restart Play mode to see counter increment from disk.");
            }
            catch (OperationCanceledException)
            {
                // shutdown during async — ignore
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TestStart] ERROR: {ex}");
            }
        }

        private static void LogSaveFile()
        {
            var path = Path.Combine(Application.persistentDataPath, "bookstore_save.json");
            Debug.Log($"[TestStart] save path: {path}");

            if (!File.Exists(path))
            {
                Debug.LogWarning("[TestStart] save file does NOT exist on disk after save call — что-то не так");
                return;
            }

            var info = new FileInfo(path);
            Debug.Log($"[TestStart] file size: {info.Length}B, lastWrite: {info.LastWriteTimeUtc:O}");

            try
            {
                var content = File.ReadAllText(path);
                var preview = content.Length > 800 ? content.Substring(0, 800) + "..." : content;
                Debug.Log($"[TestStart] file content:\n{preview}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TestStart] could not read file: {ex.Message}");
            }
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _save?.Dispose();
        }
    }
}
