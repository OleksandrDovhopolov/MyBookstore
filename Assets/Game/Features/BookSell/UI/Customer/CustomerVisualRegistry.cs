using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Book.Sell.Services;
using Cysharp.Threading.Tasks;
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
        private readonly CustomerVisual _visualPrefab;
        private readonly Transform _spawnRoot;
        private readonly Transform _entryLeft;
        private readonly Transform _entryRight;
        private readonly Transform _shopApproach;
        private readonly Transform[] _laneAnchors;
        private readonly Transform _exitLeft;
        private readonly Transform _exitRight;

        private readonly Dictionary<string, VisualState> _byId = new();
        private int _spawnedCount;

        public event Action<CustomerVisual> CustomerVisualSpawned;
        public event Action<CustomerVisual> CustomerVisualDespawned;

        public CustomerVisualRegistry(
            ISalesDayController sales,
            IObjectResolver resolver,
            SalesTuning tuning,
            CustomerVisualRegistryConfig config)
        {
            _sales = sales;
            _resolver = resolver;
            _tuning = tuning;
            _visualPrefab = config.VisualPrefab;
            _spawnRoot = config.SpawnRoot;
            _entryLeft = config.EntryLeft;
            _entryRight = config.EntryRight;
            _shopApproach = config.ShopApproach;
            _laneAnchors = config.LaneAnchors ?? Array.Empty<Transform>();
            _exitLeft = config.ExitLeft;
            _exitRight = config.ExitRight;
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
                    state.Visual.MoveToAsync(state.LanePosition, ResolveApproachDuration(customer)).Forget();
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

            var visual = _resolver.Instantiate(_visualPrefab, spawnPos, Quaternion.identity, _spawnRoot);
            visual.Initialize(customer);

            _byId[customer.Id] = new VisualState(visual, lanePos);
            CustomerVisualSpawned?.Invoke(visual);
        }

        private async Cysharp.Threading.Tasks.UniTaskVoid MoveToExitAndDespawnAsync(string customerId, VisualState state, float exitDuration)
        {
            if (state.DespawnStarted || state.Visual == null) return;
            state.DespawnStarted = true;

            await state.Visual.MoveToAsync(ResolveExitPosition(), exitDuration);
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
            var visual = state.Visual;
            CustomerVisualDespawned?.Invoke(visual);
            if (visual != null) UnityEngine.Object.Destroy(visual.gameObject);
        }

        private Vector3 ResolveEntryPosition()
        {
            var useLeft = UnityEngine.Random.value < 0.5f;
            var anchor = useLeft ? _entryLeft : _entryRight;
            if (anchor != null) return anchor.position;
            return ResolveCameraEdgePosition(useLeft);
        }

        private Vector3 ResolveExitPosition()
        {
            var useLeft = UnityEngine.Random.value < 0.5f;
            var anchor = useLeft ? _exitLeft : _exitRight;
            if (anchor != null) return anchor.position;
            return ResolveCameraEdgePosition(useLeft);
        }

        private Vector3 ResolveLanePosition(int customerIndex)
        {
            if (_laneAnchors.Length > 0)
            {
                var lane = _laneAnchors[customerIndex % _laneAnchors.Length];
                if (lane != null) return lane.position;
            }

            if (_shopApproach != null) return _shopApproach.position;
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

        private sealed class VisualState
        {
            public CustomerVisual Visual { get; }
            public Vector3 LanePosition { get; }
            public bool DespawnStarted { get; set; }

            public VisualState(CustomerVisual visual, Vector3 lanePosition)
            {
                Visual = visual;
                LanePosition = lanePosition;
            }
        }
    }

    // Config DTO passed via DI so the prefab and the optional spawn root can be wired from
    // a ScriptableObject installer on the BookSell scope without forcing the registry to know about Unity-specific lookups.
    public sealed class CustomerVisualRegistryConfig
    {
        public CustomerVisual VisualPrefab { get; }
        public Transform SpawnRoot { get; }
        public Transform EntryLeft { get; }
        public Transform EntryRight { get; }
        public Transform ShopApproach { get; }
        public Transform[] LaneAnchors { get; }
        public Transform ExitLeft { get; }
        public Transform ExitRight { get; }

        public CustomerVisualRegistryConfig(
            CustomerVisual visualPrefab,
            Transform spawnRoot = null,
            Transform entryLeft = null,
            Transform entryRight = null,
            Transform shopApproach = null,
            Transform[] laneAnchors = null,
            Transform exitLeft = null,
            Transform exitRight = null)
        {
            VisualPrefab = visualPrefab;
            SpawnRoot = spawnRoot;
            EntryLeft = entryLeft;
            EntryRight = entryRight;
            ShopApproach = shopApproach;
            LaneAnchors = laneAnchors;
            ExitLeft = exitLeft;
            ExitRight = exitRight;
        }
    }
}
