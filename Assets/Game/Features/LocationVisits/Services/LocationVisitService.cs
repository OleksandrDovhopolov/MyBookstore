using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.LocationVisits.API;
using Save;
using UnityEngine;

namespace Game.LocationVisits.Services
{
    /// <summary>
    /// Default location-visits service. Self-registers as <see cref="ISaveHook"/> and loads counts on
    /// <see cref="AfterLoadAsync"/>, like <c>SalesStatsService</c>. Batched-write policy: <see cref="RecordVisit"/>
    /// only mutates an in-memory tally and flags the save dirty via <see cref="ISaveService.MarkDirty"/>; the
    /// actual persist happens once per save cycle in <see cref="BeforeSaveAsync"/>.
    ///
    /// One instance is exposed under the read (<see cref="ILocationVisitsReader"/>), current-location
    /// (<see cref="ICurrentLocationProvider"/>) and write (<see cref="ILocationVisitService"/>) seams.
    /// </summary>
    public sealed class LocationVisitService :
        ILocationVisitService, ILocationVisitsReader, ICurrentLocationProvider, ISaveHook
    {
        private const string LogPrefix = "[LocationVisits]";

        private readonly ISaveService _save;
        private readonly ILocationVisitsRepository _repository;

        // Location ids are ordinal config ids.
        private readonly Dictionary<string, int> _visits = new(StringComparer.Ordinal);

        private bool _loaded;
        private bool _dirty;

        public LocationVisitService(ISaveService save, ILocationVisitsRepository repository)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            save.RegisterHook(this);
        }

        // Runtime-only: null at the hub, set on entry, cleared on return.
        public string CurrentLocationId { get; private set; }

        public int GetVisits(string locationId)
            => !string.IsNullOrEmpty(locationId) && _visits.TryGetValue(locationId, out var count) ? count : 0;

        public void RecordVisit(string locationId)
        {
            if (string.IsNullOrEmpty(locationId)) return;
            if (!_loaded)
                Debug.LogWarning($"{LogPrefix} RecordVisit before AfterLoadAsync; mutation will still apply.");

            _visits[locationId] = (_visits.TryGetValue(locationId, out var current) ? current : 0) + 1;
            CurrentLocationId = locationId;

            // In-memory only; the real write is deferred to the next save cycle (BeforeSaveAsync).
            _dirty = true;
            _save.MarkDirty();
        }

        public void ClearCurrentLocation() => CurrentLocationId = null;

        // ----- ISaveHook -----

        public async UniTask AfterLoadAsync(CancellationToken ct)
        {
            var dto = await _repository.LoadAsync(ct);

            _visits.Clear();
            if (dto?.VisitsByLocation != null)
                foreach (var pair in dto.VisitsByLocation)
                    if (!string.IsNullOrEmpty(pair.Key)) _visits[pair.Key] = pair.Value;

            CurrentLocationId = null; // fresh load = at the hub
            _loaded = true;
            _dirty = false;
            Debug.Log($"{LogPrefix} loaded: locations={_visits.Count}.");
        }

        public UniTask BeforeSaveAsync(CancellationToken ct)
        {
            if (!_dirty) return UniTask.CompletedTask;
            _dirty = false;
            return _repository.SaveAsync(BuildDto(), ct);
        }

        private LocationVisitsStateDto BuildDto()
            => new LocationVisitsStateDto
            {
                VisitsByLocation = new Dictionary<string, int>(_visits, StringComparer.Ordinal)
            };
    }
}
