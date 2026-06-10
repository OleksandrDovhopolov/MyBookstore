using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Game.Bootstrap.Loading
{
    // Реализация через UnityEngine.SceneManagement.SceneManager. LoadSceneMode.Single —
    // выгружает текущую сцену и загружает целевую. Целевая сцена должна быть в Build Settings.
    public sealed class SceneTransitionService : ISceneTransitionService
    {
        public async UniTask TransitionToAsync(string sceneName, IProgress<float> progress, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException("Scene name is required.", nameof(sceneName));
            }

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

            while (!asyncOp.isDone)
            {
                ct.ThrowIfCancellationRequested();
                // Unity отдаёт 0..0.9 на сам load, потом скачок до 1.0 при активации.
                // Нормализуем диапазон в 0..1 для прогресс-бара.
                var normalized = asyncOp.progress >= 0.9f ? 1f : asyncOp.progress / 0.9f;
                progress?.Report(normalized);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            progress?.Report(1f);
        }
    }
}
