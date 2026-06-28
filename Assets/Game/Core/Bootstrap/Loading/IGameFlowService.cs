using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Bootstrap.Loading
{
    // Оркестратор игрового цикла хаб ↔ локация (см. docs/GameFlowLoop.md).
    // Глобальный singleton. Поверх ISceneTransitionService + ITransitionAnimationService:
    //   EnterLocationAsync — additive-загрузка LocationScene поверх хаба;
    //   ReturnToHubAsync   — выгрузка LocationScene и возврат в хаб.
    // Реентрантность: повторные вызовы во время перехода игнорируются (IsTransitioning).
    public interface IGameFlowService
    {
        // True пока идёт переход (cover → load/unload → reveal). Защита от двойного клика.
        bool IsTransitioning { get; }

        // True, когда LocationScene загружена (игрок «в локации»).
        bool IsLocationLoaded { get; }

        // Регистрирует корневой GameObject хаба, который гасится при входе в локацию.
        // Вызывается scene-компонентом хаба (через DI из GameplayLifetimeScope).
        void RegisterHubRoot(GameObject hubRoot);

        // Хаб → локация: cover → LoadAdditive(LocationScene, parent=Global) → выключить hub root → reveal.
        UniTask EnterLocationAsync(CancellationToken ct = default);

        // Локация → хаб: cover → Unload(LocationScene) → включить hub root → SetActive(хаб) → reveal.
        UniTask ReturnToHubAsync(CancellationToken ct = default);
    }
}
