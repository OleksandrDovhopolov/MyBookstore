using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using UnityEngine;

namespace Book.Sell.Services
{
    public sealed class CustomerSpawnScheduler
    {
        private readonly IReadOnlyList<Customer> _customers;
        private readonly SalesTuning _tuning;
        private readonly int[] _waveEndExclusive;
        private readonly float _waveGapSeconds;

        private float _spawnTimer;
        private int _nextToSpawn;
        private int _currentWaveIndex;
        private float _waveGapTimer;
        private bool _spawningStopped;

        public CustomerSpawnScheduler(
            IReadOnlyList<Customer> customers,
            IReadOnlyList<int> waveSizes,
            float waveGapSeconds,
            SalesTuning tuning)
        {
            _customers = customers ?? throw new ArgumentNullException(nameof(customers));
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            _waveEndExclusive = BuildWaveEndExclusive(waveSizes, customers.Count);
            _waveGapSeconds = Mathf.Max(0f, waveGapSeconds);
            _spawnTimer = tuning.SpawnInterval;
        }

        public int SpawnedCount => _nextToSpawn;
        public bool NoMoreToSpawn => _nextToSpawn >= _customers.Count || _spawningStopped;

        public void Advance(float dt)
        {
            if (_spawningStopped) return;
            if (_nextToSpawn >= _customers.Count) return;
            if (!TryOpenCurrentWave(dt)) return;

            var waveEnd = CurrentWaveEndExclusive();
            _spawnTimer += dt;
            while (_spawnTimer >= _tuning.SpawnInterval
                   && _nextToSpawn < waveEnd
                   && !IsConcurrencyCapReached())
            {
                _spawnTimer -= _tuning.SpawnInterval;
                _nextToSpawn++;
            }
        }

        public void StopSpawning()
        {
            _spawningStopped = true;
        }

        private bool TryOpenCurrentWave(float dt)
        {
            if (_waveEndExclusive.Length == 0) return true;

            var waveEnd = CurrentWaveEndExclusive();
            if (_nextToSpawn < waveEnd) return true;
            if (_nextToSpawn >= _customers.Count) return true;
            if (!AllSpawnedCustomersDone(waveEnd)) return false;

            _waveGapTimer += Mathf.Max(0f, dt);
            if (_waveGapTimer < _waveGapSeconds) return false;

            _currentWaveIndex = Mathf.Min(_currentWaveIndex + 1, _waveEndExclusive.Length - 1);
            _waveGapTimer = 0f;
            _spawnTimer = _tuning.SpawnInterval;
            return true;
        }

        private int CurrentWaveEndExclusive()
        {
            if (_waveEndExclusive.Length == 0) return _customers.Count;

            var index = Mathf.Clamp(_currentWaveIndex, 0, _waveEndExclusive.Length - 1);
            return _waveEndExclusive[index];
        }

        private bool AllSpawnedCustomersDone(int exclusiveEnd)
        {
            for (var i = 0; i < exclusiveEnd; i++)
            {
                if (!_customers[i].IsDone) return false;
            }

            return true;
        }

        private bool IsConcurrencyCapReached()
        {
            var cap = _tuning.MaxConcurrentCustomers;
            if (cap <= 0) return false;
            return ActiveCustomerCount() >= cap;
        }

        private int ActiveCustomerCount()
        {
            var count = 0;
            for (var i = 0; i < _nextToSpawn; i++)
            {
                if (!_customers[i].IsDone) count++;
            }

            return count;
        }

        private static int[] BuildWaveEndExclusive(IReadOnlyList<int> waveSizes, int customerCount)
        {
            if (customerCount <= 0) return Array.Empty<int>();
            if (waveSizes == null || waveSizes.Count == 0) return new[] { customerCount };

            for (var i = 0; i < waveSizes.Count; i++)
            {
                if (waveSizes[i] <= 0) return new[] { customerCount };
            }

            var ends = new List<int>();
            var consumed = 0;
            for (var i = 0; i < waveSizes.Count && consumed < customerCount; i++)
            {
                consumed = Mathf.Min(customerCount, consumed + waveSizes[i]);
                ends.Add(consumed);
            }

            if (consumed < customerCount)
                ends.Add(customerCount);

            return ends.Count > 0 ? ends.ToArray() : new[] { customerCount };
        }
    }
}
