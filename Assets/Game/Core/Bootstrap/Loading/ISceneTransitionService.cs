using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Game.Bootstrap.Loading
{
    // Generic-сервис переходов между сценами. Не привязан к bootstrap-флоу —
    // может вызываться из любого места (gameplay → preparation, hub → location и т.п.).
    //
    // Сервис намеренно НЕ знает про VContainer/LifetimeScope: установку родителя
    // scene-скопа (LifetimeScope.EnqueueParent) делает вызывающий код (GameFlowService),
    // оборачивая вызов LoadAdditiveAsync. Так Loading-сборка не тащит UI/DI-зависимости.
    public interface ISceneTransitionService
    {
        // Single-режим: выгружает текущую сцену и грузит целевую. Используется бутстрапом.
        UniTask TransitionToAsync(string sceneName, IProgress<float> progress, CancellationToken ct);

        // Additive-режим: грузит сцену поверх текущей, не выгружая её. Возвращает загруженную Scene.
        // Если makeActive=true — делает её активной (важно для Instantiate/lighting в этой сцене).
        UniTask<Scene> LoadAdditiveAsync(string sceneName, bool makeActive, IProgress<float> progress, CancellationToken ct);

        // Выгружает ранее загруженную additive-сцену.
        UniTask UnloadAsync(string sceneName, CancellationToken ct);

        // Делает указанную (уже загруженную) сцену активной. No-op, если сцена не найдена/не валидна.
        void SetActiveScene(string sceneName);
    }
}
