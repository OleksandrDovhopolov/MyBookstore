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
        private readonly CustomerVisual _visualPrefab;
        private readonly ILocationContext _location;

        private readonly Dictionary<string, VisualState> _byId = new();
        private int _spawnedCount;

        public event Action<CustomerVisual> CustomerVisualSpawned;
        public event Action<CustomerVisual> CustomerVisualDespawned;

        public CustomerVisualRegistry(
            ISalesDayController sales,
            IObjectResolver resolver,
            SalesTuning tuning,
            CustomerVisualRegistryConfig config,
            IUiSpriteProvider uiSprites = null,
            IRecommendationMinigamePresenter minigamePresenter = null)
        {
            _sales = sales;
            _resolver = resolver;
            _tuning = tuning;
            _isPaused = () => minigamePresenter?.IsWindowOpen ?? false;
            _uiSprites = uiSprites;
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
                    state.Visual.MoveToAsync(state.LanePosition, ResolveApproachDuration(customer), _isPaused).Forget();
                    break;
                case CustomerPhase.Leaving:
                    MoveToExitAndDespawnAsync(customer.Id, state, ResolveLeaveDuration(customer)).Forget();
                    break;
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
            var lanePos = ResolveLanePosition(_spawnedCount);
            _spawnedCount++;

            var visual = _resolver.Instantiate(_visualPrefab, spawnPos, Quaternion.identity, _location?.CustomerSpawnRoot);
            visual.Initialize(customer);

            var state = new VisualState(visual, lanePos);
            _byId[customer.Id] = state;
            LoadCharacterSpriteAsync(customer.CharacterId, state).Forget();
            CustomerVisualSpawned?.Invoke(visual);
        }

        private async UniTaskVoid LoadCharacterSpriteAsync(string characterId, VisualState state)
        {
            if (state == null)
                return;

            if (_uiSprites == null)
            {
                ApplyFallbackSprite(state.Visual);
                return;
            }

            if (string.IsNullOrWhiteSpace(characterId))
            {
                ApplyFallbackSprite(state.Visual);
                return;
            }

            try
            {
                var token = state.SpriteToken;
                var sprite = await _uiSprites.GetSpriteAsync(characterId, token);
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
                Debug.LogWarning($"[CustomerVisualRegistry] Failed to load character sprite '{characterId}': {ex.Message}");
            }
        }

        private async Cysharp.Threading.Tasks.UniTaskVoid MoveToExitAndDespawnAsync(string customerId, VisualState state, float exitDuration)
        {
            if (state.DespawnStarted || state.Visual == null) return;
            state.DespawnStarted = true;

            await state.Visual.MoveToAsync(ResolveExitPosition(), exitDuration, _isPaused);
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

        private Vector3 ResolveLanePosition(int customerIndex)
        {
            var laneAnchors = _location?.LaneAnchors;
            if (laneAnchors is { Count: > 0 })
            {
                var lane = laneAnchors[customerIndex % laneAnchors.Count];
                if (lane != null) return lane.position;
            }

            var shopApproach = _location?.ShopApproach;
            if (shopApproach != null) return shopApproach.position;
            return Vector3.zero;
        }

        private float ResolveApproachDuration(Book.Sell.Domain.Customer customer)
            => customer.CurrentStep is ApproachStep approachStep
                ? approachStep.ResolveDuration(_tuning)
                : _tuning.ApproachDuration;

        private float ResolveLeaveDuration(Book.Sell.Domain.Customer customer)
            => customer.CurrentStep is LeaveStep leaveStep
                ? leaveStep.ResolveDuration(_tuning)
                : _tuning.LeaveDuration;

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
