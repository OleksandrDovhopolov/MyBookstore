using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using Book.Sell.Services;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Book.Sell.UI.Customer
{
    // Listens to ISalesDayController.CustomerPhaseChanged. On the first phase change for a customer
    // it instantiates a CustomerVisual placeholder prefab at a hardcoded position; on Done it
    // schedules destroy. Phase 0 places customers in a simple horizontal row.
    public sealed class CustomerVisualRegistry : ICustomerVisualRegistry, IStartable, IDisposable
    {
        private const float SpawnSpacingX = 1.5f;
        private const float DespawnDelaySeconds = 2f;

        private readonly ISalesDayController _sales;
        private readonly IObjectResolver _resolver;
        private readonly CustomerVisual _visualPrefab;
        private readonly Transform _spawnRoot;

        private readonly Dictionary<string, CustomerVisual> _byId = new();
        private int _spawnedCount;

        public event Action<CustomerVisual> CustomerVisualSpawned;
        public event Action<CustomerVisual> CustomerVisualDespawned;

        public CustomerVisualRegistry(
            ISalesDayController sales,
            IObjectResolver resolver,
            CustomerVisualRegistryConfig config)
        {
            _sales = sales;
            _resolver = resolver;
            _visualPrefab = config.VisualPrefab;
            _spawnRoot = config.SpawnRoot;
        }

        public CustomerVisual GetById(string customerId)
            => customerId != null && _byId.TryGetValue(customerId, out var v) ? v : null;

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

            if (customer.Phase == CustomerPhase.Done)
            {
                _ = DespawnDelayedAsync(customer.Id);
            }
        }

        private void Spawn(Book.Sell.Domain.Customer customer)
        {
            if (_visualPrefab == null)
            {
                Debug.LogWarning("[CustomerVisualRegistry] No CustomerVisual prefab configured.");
                return;
            }

            var spawnPos = new Vector3(_spawnedCount * SpawnSpacingX, 0f, 0f);
            _spawnedCount++;

            var visual = _resolver.Instantiate(_visualPrefab, spawnPos, Quaternion.identity, _spawnRoot);
            visual.Initialize(customer);

            _byId[customer.Id] = visual;
            CustomerVisualSpawned?.Invoke(visual);
        }

        private async Cysharp.Threading.Tasks.UniTaskVoid DespawnDelayedAsync(string customerId)
        {
            await Cysharp.Threading.Tasks.UniTask.Delay(TimeSpan.FromSeconds(DespawnDelaySeconds));

            if (!_byId.Remove(customerId, out var visual)) return;
            CustomerVisualDespawned?.Invoke(visual);
            if (visual != null) UnityEngine.Object.Destroy(visual.gameObject);
        }
    }

    // Config DTO passed via DI so the prefab and the optional spawn root can be wired from
    // a ScriptableObject installer on the BookSell scope without forcing the registry to know about Unity-specific lookups.
    public sealed class CustomerVisualRegistryConfig
    {
        public CustomerVisual VisualPrefab { get; }
        public Transform SpawnRoot { get; }

        public CustomerVisualRegistryConfig(CustomerVisual visualPrefab, Transform spawnRoot = null)
        {
            VisualPrefab = visualPrefab;
            SpawnRoot = spawnRoot;
        }
    }
}
