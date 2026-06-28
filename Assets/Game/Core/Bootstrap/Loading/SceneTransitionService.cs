using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Bootstrap.Loading
{
    // Реализация через UnityEngine.SceneManagement.SceneManager.
    // TransitionToAsync — LoadSceneMode.Single (выгружает текущую сцену).
    // LoadAdditiveAsync / UnloadAsync — LoadSceneMode.Additive для hub ↔ location цикла.
    // Целевые сцены должны быть в Build Settings.
    public sealed class SceneTransitionService : ISceneTransitionService
    {
        private const string LogPrefix = "[SceneTransition]";

        public async UniTask TransitionToAsync(string sceneName, IProgress<float> progress, CancellationToken ct)
        {
            EnsureSceneName(sceneName);
            ct.ThrowIfCancellationRequested();

            // Кадр на отрисовку финального состояния прогресс-бара перед началом выгрузки.
            await UniTask.Yield(PlayerLoopTiming.Update, ct);

            var asyncOp = SceneManager.LoadSceneAsync(sceneName);
            if (asyncOp == null)
            {
                throw new InvalidOperationException(
                    $"SceneManager.LoadSceneAsync returned null for scene '{sceneName}'. " +
                    "Проверь, что сцена добавлена в Build Settings (File → Build Profiles).");
            }

            await AwaitOpAsync(asyncOp, progress, ct);
            progress?.Report(1f);
        }

        public async UniTask<Scene> LoadAdditiveAsync(string sceneName, bool makeActive, IProgress<float> progress, CancellationToken ct)
        {
            EnsureSceneName(sceneName);
            ct.ThrowIfCancellationRequested();

            await UniTask.Yield(PlayerLoopTiming.Update, ct);

            var asyncOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (asyncOp == null)
            {
                throw new InvalidOperationException(
                    $"SceneManager.LoadSceneAsync(Additive) returned null for scene '{sceneName}'. " +
                    "Проверь, что сцена добавлена в Build Settings.");
            }

            await AwaitOpAsync(asyncOp, progress, ct);
            progress?.Report(1f);

            var scene = SceneManager.GetSceneByName(sceneName);
            if (makeActive && scene.IsValid())
            {
                SceneManager.SetActiveScene(scene);
            }

            return scene;
        }

        public async UniTask UnloadAsync(string sceneName, CancellationToken ct)
        {
            EnsureSceneName(sceneName);
            ct.ThrowIfCancellationRequested();

            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning($"{LogPrefix} UnloadAsync skipped — scene '{sceneName}' is not loaded.");
                return;
            }

            var asyncOp = SceneManager.UnloadSceneAsync(scene);
            if (asyncOp == null)
            {
                Debug.LogWarning($"{LogPrefix} UnloadSceneAsync returned null for '{sceneName}'.");
                return;
            }

            await AwaitOpAsync(asyncOp, progress: null, ct);
        }

        public void SetActiveScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) return;

            var scene = SceneManager.GetSceneByName(sceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(scene);
            }
            else
            {
                Debug.LogWarning($"{LogPrefix} SetActiveScene skipped — scene '{sceneName}' is not loaded.");
            }
        }

        private static void EnsureSceneName(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                throw new ArgumentException("Scene name is required.", nameof(sceneName));
        }

        private static async UniTask AwaitOpAsync(AsyncOperation asyncOp, IProgress<float> progress, CancellationToken ct)
        {
            while (!asyncOp.isDone)
            {
                ct.ThrowIfCancellationRequested();
                // Unity отдаёт 0..0.9 на сам load, потом скачок до 1.0 при активации.
                // Нормализуем диапазон в 0..1 для прогресс-бара.
                var normalized = asyncOp.progress >= 0.9f ? 1f : asyncOp.progress / 0.9f;
                progress?.Report(normalized);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }
    }
}
