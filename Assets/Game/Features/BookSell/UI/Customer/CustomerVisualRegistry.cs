using System;
using System.Collections.Generic;
using System.Threading;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Book.Sell.Services;
using Cysharp.Threading.Tasks;
using Game.Location.API;
using SpriteService;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Book.Sell.UI.Customer
{
    // Listens to ISalesDayController.CustomerPhaseChanged. On the first phase change for a customer
    // it instantiates a CustomerVisual placeholder prefab at a location-authored entry anchor,
    // moves it to a lane near the shop, then sends it to a random exit when the domain enters Leaving.
    public sealed class CustomerVisualRegistry : ICustomerVisualRegistry, IStartable, IDisposable
    {
        private const float CameraFallbackHorizontalMargin = 1f;
        private const float DespawnDelaySeconds = 2f;

        private readonly ISalesDayController _sales;
        private readonly IObjectResolver _resolver;
        private readonly SalesTuning _tuning;
        // Same pause source the day loop uses (SalesScreenView gates Tick on it): freezes customer
        // movement while the minigame window is open so visuals don't drift past the paused logic.
        private readonly Func<bool> _isPaused;
        private readonly IUiSpriteProvider _uiSprites;
        private readonly ICustomerVisualSpriteResolver _spriteResolver;
        private readonly CustomerVisual _visualPrefab;
        private readonly ILocationContext _location;
        private readonly ILaneSlotAllocator _lanes;

        private readonly Dictionary<string, VisualState> _byId = new();
        private readonly HashSet<string> _calibrationLogged = new();
        private int _spawnedCount;

        public event Action<CustomerVisual> CustomerVisualSpawned;
        public event Action<CustomerVisual> CustomerVisualDespawned;

        public CustomerVisualRegistry(
            ISalesDayController sales,
            IObjectResolver resolver,
            SalesTuning tuning,
            CustomerVisualRegistryConfig config,
            ILaneSlotAllocator lanes = null,
            IUiSpriteProvider uiSprites = null,
            ICustomerVisualSpriteResolver spriteResolver = null,
            IRecommendationMinigamePresenter minigamePresenter = null,
            IInteractionLock interactionLock = null)
        {
            _sales = sales;
            _resolver = resolver;
            _tuning = tuning;
            _lanes = lanes;
            // Both domain pause gates, not just the minigame window: SalesDayController also stops
            // ticking while IInteractionLock is held (scripted dialogue, tutorial callouts). Missing the
            // lock let visuals keep walking while the domain stood still.
            _isPaused = () => (minigamePresenter?.IsWindowOpen ?? false)
                              || (interactionLock?.IsHeld ?? false);
            _uiSprites = uiSprites;
            _spriteResolver = spriteResolver;
            _visualPrefab = config?.VisualPrefab;
            _location = config?.Location;
        }

        public CustomerVisual GetById(string customerId)
            => customerId != null && _byId.TryGetValue(customerId, out var state) ? state.Visual : null;

        public void Start()
        {
            _sales.CustomerPhaseChanged += OnCustomerPhaseChanged;
        }

        public void Dispose()
        {
            _sales.CustomerPhaseChanged -= OnCustomerPhaseChanged;

            foreach (var state in _byId.Values)
                state.Dispose();
            _byId.Clear();
        }

        private void OnCustomerPhaseChanged(Book.Sell.Domain.Customer customer)
        {
            if (!_byId.ContainsKey(customer.Id))
            {
                Spawn(customer);
            }

            if (!_byId.TryGetValue(customer.Id, out var state) || state.Visual == null)
                return;

            switch (customer.Phase)
            {
                case CustomerPhase.Approaching:
                {
                    var approachFrom = state.Visual.transform.position;
                    var approachDuration = ResolveApproachDuration(customer, approachFrom, state.LanePosition);
                    state.Visual.MoveToAsync(state.LanePosition, approachDuration, _isPaused).Forget();
                    break;
                }
                case CustomerPhase.Leaving:
                {
                    var exitPosition = ResolveExitPosition();
                    var leaveFrom = state.Visual.transform.position;
                    var leaveDuration = ResolveLeaveDuration(customer, leaveFrom, exitPosition);
                    MoveToExitAndDespawnAsync(customer.Id, state, exitPosition, leaveDuration).Forget();
                    break;
                }
                case CustomerPhase.Done:
                    if (!state.DespawnStarted)
                        DespawnDelayedAsync(customer.Id).Forget();
                    break;
            }
        }

        private void Spawn(Book.Sell.Domain.Customer customer)
        {
            if (_visualPrefab == null)
            {
                Debug.LogWarning("[CustomerVisualRegistry] No CustomerVisual prefab configured.");
                return;
            }

            var spawnPos = ResolveEntryPosition();
            var lanePos = ResolveLanePosition(customer.Id);
            _spawnedCount++;

            var visual = _resolver.Instantiate(_visualPrefab, spawnPos, Quaternion.identity, _location?.CustomerSpawnRoot);
            visual.Initialize(customer);

            var state = new VisualState(visual, lanePos);
            _byId[customer.Id] = state;
            LoadFigureSpriteAsync(customer, state).Forget();
            CustomerVisualSpawned?.Invoke(visual);
        }

        private async UniTaskVoid LoadFigureSpriteAsync(Book.Sell.Domain.Customer customer, VisualState state)
        {
            if (state == null)
                return;

            if (_uiSprites == null)
            {
                ApplyFallbackSprite(state.Visual);
                return;
            }

            var spriteKey = _spriteResolver?.ResolveFigureSpriteKey(customer) ?? customer?.CharacterId;
            if (string.IsNullOrWhiteSpace(spriteKey))
            {
                ApplyFallbackSprite(state.Visual);
                return;
            }

            try
            {
                var token = state.SpriteToken;
                var sprite = await _uiSprites.GetSpriteAsync(spriteKey, token);
                if (token.IsCancellationRequested)
                    return;

                if (state.Visual == null)
                    return;

                state.Visual.ApplyFigureSprite(sprite);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CustomerVisualRegistry] Failed to load customer figure sprite '{spriteKey}': {ex.Message}");
            }
        }

        private async Cysharp.Threading.Tasks.UniTaskVoid MoveToExitAndDespawnAsync(
            string customerId, VisualState state, Vector3 exitPosition, float exitDuration)
        {
            if (state.DespawnStarted || state.Visual == null) return;
            state.DespawnStarted = true;

            await state.Visual.MoveToAsync(exitPosition, exitDuration, _isPaused);
            DespawnNow(customerId);
        }

        private async Cysharp.Threading.Tasks.UniTaskVoid DespawnDelayedAsync(string customerId)
        {
            await Cysharp.Threading.Tasks.UniTask.Delay(TimeSpan.FromSeconds(DespawnDelaySeconds));
            DespawnNow(customerId);
        }

        private void DespawnNow(string customerId)
        {
            if (!_byId.Remove(customerId, out var state)) return;
            _lanes?.Release(customerId);
            state.Dispose();
            var visual = state.Visual;
            CustomerVisualDespawned?.Invoke(visual);
            if (visual != null) UnityEngine.Object.Destroy(visual.gameObject);
        }

        private Vector3 ResolveEntryPosition()
        {
            var useLeft = UnityEngine.Random.value < 0.5f;
            var anchor = useLeft ? _location?.EntryLeft : _location?.EntryRight;
            if (anchor != null) return anchor.position;
            return ResolveCameraEdgePosition(useLeft);
        }

        private Vector3 ResolveExitPosition()
        {
            var useLeft = UnityEngine.Random.value < 0.5f;
            var anchor = useLeft ? _location?.ExitLeft : _location?.ExitRight;
            if (anchor != null) return anchor.position;
            return ResolveCameraEdgePosition(useLeft);
        }

        private Vector3 ResolveLanePosition(string customerId)
        {
            var lane = _lanes?.Acquire(customerId);
            if (lane != null) return lane.position;

            var shopApproach = _location?.ShopApproach;
            if (shopApproach != null) return shopApproach.position;
            return Vector3.zero;
        }

        /// <summary>
        /// Turns the real walking distance into a duration so every customer moves at the same visible
        /// speed, and pushes it back into the domain step - the step decides when buying starts, so the
        /// two must agree. The push is safe here: this runs inside ApproachStep.Enter, before the step
        /// has consumed any time. Falls back to the duration drawn at plan-build time when speed is off
        /// or there is no step to push into (tests, headless).
        /// </summary>
        private float ResolveApproachDuration(Book.Sell.Domain.Customer customer, Vector3 from, Vector3 to)
        {
            if (customer.CurrentStep is not ApproachStep approachStep)
                return _tuning.ApproachDuration;

            var byDistance = DurationFromDistance(
                from, to, _tuning.ApproachSpeed, _tuning.MinApproachDuration, _tuning.MaxApproachDuration);

            if (byDistance.HasValue && approachStep.TryOverrideDuration(byDistance.Value))
                LogMovementCalibration("approach", from, to, byDistance.Value);

            return approachStep.ResolveDuration(_tuning);
        }

        private float ResolveLeaveDuration(Book.Sell.Domain.Customer customer, Vector3 from, Vector3 to)
        {
            if (customer.CurrentStep is not LeaveStep leaveStep)
                return _tuning.LeaveDuration;

            var byDistance = DurationFromDistance(
                from, to, _tuning.LeaveSpeed, _tuning.MinLeaveDuration, _tuning.MaxLeaveDuration);

            if (byDistance.HasValue && leaveStep.TryOverrideDuration(byDistance.Value))
                LogMovementCalibration("leave", from, to, byDistance.Value);

            return leaveStep.ResolveDuration(_tuning);
        }

        private static float? DurationFromDistance(Vector3 from, Vector3 to, float speed, float min, float max)
        {
            if (speed <= 0f) return null;

            var distance = Vector3.Distance(from, to);
            if (distance <= Mathf.Epsilon) return null;

            var lower = Mathf.Max(0.01f, min);
            var upper = Mathf.Max(lower, max);
            return Mathf.Clamp(distance / speed, lower, upper);
        }

        /// <summary>
        /// One line per phase per session, so the authored speeds can be calibrated against the real
        /// scene scale: if every duration pins to a clamp, the speed is wrong for this location.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogMovementCalibration(string phase, Vector3 from, Vector3 to, float duration)
        {
            if (!_calibrationLogged.Add(phase)) return;
            Debug.Log($"[CustomerVisualRegistry] {phase}: distance={Vector3.Distance(from, to):F2} units, " +
                      $"duration={duration:F2}s. Tune the matching speed so this lands between min and max.");
        }

        private static Vector3 ResolveCameraEdgePosition(bool left)
        {
            var camera = Camera.main;
            if (camera == null || !camera.orthographic)
                return new Vector3(left ? -4.5f : 4.5f, 0f, 0f);

            var halfHeight = camera.orthographicSize;
            var halfWidth = halfHeight * camera.aspect;
            var x = (left ? -halfWidth : halfWidth) + (left ? -CameraFallbackHorizontalMargin : CameraFallbackHorizontalMargin);
            return new Vector3(x, camera.transform.position.y, 0f);
        }

        private static void ApplyFallbackSprite(CustomerVisual visual)
        {
            if (visual != null)
                visual.ApplyFigureSprite(null);
        }

        private sealed class VisualState
        {
            private readonly CancellationTokenSource _spriteCts = new();

            public CustomerVisual Visual { get; }
            public Vector3 LanePosition { get; }
            public bool DespawnStarted { get; set; }
            public CancellationToken SpriteToken => _spriteCts.Token;

            public VisualState(CustomerVisual visual, Vector3 lanePosition)
            {
                Visual = visual;
                LanePosition = lanePosition;
            }

            public void Dispose()
            {
                _spriteCts.Cancel();
                _spriteCts.Dispose();
            }
        }
    }

    // Config DTO passed via DI so the prefab and the optional spawn root can be wired from
    // a ScriptableObject installer on the BookSell scope without forcing the registry to know about Unity-specific lookups.
    public sealed class CustomerVisualRegistryConfig
    {
        public CustomerVisual VisualPrefab { get; }
        public ILocationContext Location { get; }

        public CustomerVisualRegistryConfig(
            CustomerVisual visualPrefab,
            ILocationContext location = null)
        {
            VisualPrefab = visualPrefab;
            Location = location;
        }
    }
}
