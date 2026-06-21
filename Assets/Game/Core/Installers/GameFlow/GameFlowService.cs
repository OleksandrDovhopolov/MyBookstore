using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    // Реализация игрового цикла хаб ↔ локация (см. docs/GameFlowLoop.md).
    // Глобальный singleton (регистрируется в BootstrapInstaller).
    //
    // Ключевая деталь additive-загрузки: LocationLifetimeScope должен стать ребёнком
    // GlobalLifetimeScope (а не GameplayLifetimeScope). Для этого LoadAdditiveAsync оборачивается в
    // using (LifetimeScope.EnqueueParent(global)) — VContainer 1.16.x подхватывает override-родителя,
    // когда scene-скоп просыпается во время активации сцены (внутри await LoadAdditiveAsync).
    public sealed class GameFlowService : IGameFlowService
    {
        private const string LogPrefix = "[GameFlow]";

        private readonly ISceneTransitionService _sceneTransition;
        private readonly ITransitionAnimationService _animation;
        private readonly GameFlowSettings _settings;

        private GameObject _hubRoot;
        private LifetimeScope _globalScope;
        private bool _isTransitioning;
        private bool _locationLoaded;

        public GameFlowService(
            ISceneTransitionService sceneTransition,
            ITransitionAnimationService animation,
            GameFlowSettings settings)
        {
            _sceneTransition = sceneTransition ?? throw new ArgumentNullException(nameof(sceneTransition));
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public bool IsTransitioning => _isTransitioning;
        public bool IsLocationLoaded => _locationLoaded;

        public void RegisterHubRoot(GameObject hubRoot)
        {
            _hubRoot = hubRoot;
        }

        public async UniTask EnterLocationAsync(CancellationToken ct = default)
        {
            // TODO (отдельная задача — FTUE/обучение): здесь будет ветка «первый вход» —
            // загрузить LocationScene и запустить tutorial. Сейчас всегда обычный путь.
            if (!TryBeginTransition(nameof(EnterLocationAsync))) return;

            try
            {
                if (_locationLoaded)
                {
                    Debug.LogWarning($"{LogPrefix} EnterLocation ignored — location already loaded.");
                    return;
                }

                await _animation.PlayCoverAsync(ct);

                var global = ResolveGlobalScope();
                using (LifetimeScope.EnqueueParent(global))
                {
                    await _sceneTransition.LoadAdditiveAsync(
                        _settings.LocationSceneName, makeActive: true, progress: null, ct);
                }

                SetHubRootActive(false);
                _locationLoaded = true;

                await _animation.PlayRevealAsync(ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogPrefix} EnterLocationAsync failed: {e}");
                // Best-effort recovery: не оставлять игрока на погашенном хабе.
                SetHubRootActive(true);
                throw;
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        public async UniTask ReturnToHubAsync(CancellationToken ct = default)
        {
            if (!TryBeginTransition(nameof(ReturnToHubAsync))) return;

            try
            {
                if (!_locationLoaded)
                {
                    Debug.LogWarning($"{LogPrefix} ReturnToHub ignored — no location loaded.");
                    return;
                }

                await _animation.PlayCoverAsync(ct);

                await _sceneTransition.UnloadAsync(_settings.LocationSceneName, ct);
                _sceneTransition.SetActiveScene(_settings.GameplaySceneName);
                SetHubRootActive(true);
                _locationLoaded = false;

                await _animation.PlayRevealAsync(ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogPrefix} ReturnToHubAsync failed: {e}");
                // Best-effort recovery: хаб должен быть виден.
                SetHubRootActive(true);
                throw;
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        private bool TryBeginTransition(string caller)
        {
            if (_isTransitioning)
            {
                Debug.LogWarning($"{LogPrefix} {caller} ignored — a transition is already in progress.");
                return false;
            }

            _isTransitioning = true;
            return true;
        }

        private LifetimeScope ResolveGlobalScope()
        {
            if (_globalScope != null) return _globalScope;

            _globalScope = LifetimeScope.Find<GlobalLifetimeScope>();
            if (_globalScope == null)
            {
                throw new InvalidOperationException(
                    $"{LogPrefix} GlobalLifetimeScope not found — cannot parent LocationLifetimeScope.");
            }

            return _globalScope;
        }

        private void SetHubRootActive(bool active)
        {
            if (_hubRoot != null)
            {
                _hubRoot.SetActive(active);
            }
            else
            {
                Debug.LogWarning($"{LogPrefix} hub root is not registered — skipping SetActive({active}). " +
                                 "Проверь, что в GameplayScene есть компонент, вызывающий RegisterHubRoot.");
            }
        }
    }
}
