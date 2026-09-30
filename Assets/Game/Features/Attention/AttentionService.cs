using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Attention.API;
using Save;

namespace Game.Attention
{
    /// <summary>
    /// Shared implementation of <see cref="IAttentionService"/>. Features contribute their keys as
    /// <see cref="IAttentionSource"/> (ids persisted here) or <see cref="IAttentionFlagSource"/>
    /// (seen-state owned elsewhere); this class owns only the seen-sets, the save module and the
    /// edge-triggered <see cref="Changed"/> notification.
    /// <para>
    /// Both collections default to null because VContainer throws when resolving a collection with
    /// zero registrations — a partial scope (EditMode tests) must still be able to build the service.
    /// </para>
    /// </summary>
    public sealed class AttentionService : IAttentionService, ISaveHook, IDisposable
    {
        private readonly ISaveService _save;
        private readonly IAttentionRepository _repository;
        private readonly Dictionary<string, IAttentionSource> _sources = new(StringComparer.Ordinal);
        private readonly Dictionary<string, IAttentionFlagSource> _flagSources = new(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _lastUnseen = new(StringComparer.Ordinal);

        private SavedAttention _saved = new();
        private bool _dirty;
        private bool _loaded;
        private bool _subscribed;

        public AttentionService(
            ISaveService save,
            IAttentionRepository repository,
            IReadOnlyList<IAttentionSource> sources = null,
            IReadOnlyList<IAttentionFlagSource> flagSources = null)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));

            if (sources != null)
            {
                for (var i = 0; i < sources.Count; i++)
                    Register(_sources, sources[i], sources[i]?.Key);
            }

            if (flagSources != null)
            {
                for (var i = 0; i < flagSources.Count; i++)
                    Register(_flagSources, flagSources[i], flagSources[i]?.Key);
            }

            _save.RegisterHook(this);
        }

        public event Action Changed;

        public bool HasUnseen(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;

            if (_sources.TryGetValue(key, out var source))
                return ContainsUnseen(source.CurrentIds, SeenSet(key));

            return _flagSources.TryGetValue(key, out var flagSource) && flagSource.HasUnseen;
        }

        public bool HasAnyUnseen(IReadOnlyList<string> keys)
        {
            if (keys == null) return false;

            for (var i = 0; i < keys.Count; i++)
                if (HasUnseen(keys[i]))
                    return true;

            return false;
        }

        public void MarkSeen(string key)
        {
            if (string.IsNullOrEmpty(key)) return;

            if (_sources.TryGetValue(key, out var source))
            {
                // Id sources own their seen-set here, so a real change must be persisted.
                if (AddAll(SeenSet(key), source.CurrentIds))
                    SetDirty();
            }
            else if (_flagSources.TryGetValue(key, out var flagSource))
            {
                // Flag sources persist elsewhere — never dirty this module for them.
                flagSource.MarkSeen();
            }
            else
            {
                return;
            }

            NotifyIfChanged();
        }

        public async UniTask AfterLoadAsync(CancellationToken ct)
        {
            _saved = await _repository.LoadAsync(ct) ?? new SavedAttention();
            _saved.SeenIdsByKey ??= new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            _loaded = true;
            Subscribe();
            CaptureSnapshot();
        }

        public UniTask BeforeSaveAsync(CancellationToken ct)
        {
            if (!_dirty) return UniTask.CompletedTask;
            _dirty = false;
            return _repository.SaveAsync(_saved, ct);
        }

        public void Dispose() => Unsubscribe();

        private HashSet<string> SeenSet(string key)
        {
            _saved ??= new SavedAttention();
            _saved.SeenIdsByKey ??= new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            if (_saved.SeenIdsByKey.TryGetValue(key, out var set) && set != null)
                return set;

            set = new HashSet<string>(StringComparer.Ordinal);
            _saved.SeenIdsByKey[key] = set;
            return set;
        }

        private static void Register<T>(Dictionary<string, T> target, T source, string key) where T : class
        {
            if (source == null || string.IsNullOrEmpty(key)) return;
            target[key] = source;
        }

        private static bool ContainsUnseen(IEnumerable<string> current, HashSet<string> seen)
        {
            if (current == null) return false;

            foreach (var id in current)
                if (!string.IsNullOrEmpty(id) && !seen.Contains(id))
                    return true;

            return false;
        }

        private static bool AddAll(HashSet<string> target, IEnumerable<string> ids)
        {
            if (ids == null) return false;

            var changed = false;
            foreach (var id in ids)
                if (!string.IsNullOrEmpty(id))
                    changed |= target.Add(id);

            return changed;
        }

        private void Subscribe()
        {
            if (_subscribed) return;

            foreach (var source in _sources.Values)
                source.Changed += OnSourceChanged;

            foreach (var flagSource in _flagSources.Values)
                flagSource.Changed += OnSourceChanged;

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;

            foreach (var source in _sources.Values)
                source.Changed -= OnSourceChanged;

            foreach (var flagSource in _flagSources.Values)
                flagSource.Changed -= OnSourceChanged;

            _subscribed = false;
        }

        private void OnSourceChanged() => NotifyIfChanged();

        private void NotifyIfChanged()
        {
            if (!_loaded) return;

            var changed = false;

            foreach (var key in _sources.Keys)
                changed |= UpdateSnapshot(key);

            foreach (var key in _flagSources.Keys)
                changed |= UpdateSnapshot(key);

            if (changed)
                Changed?.Invoke();
        }

        private bool UpdateSnapshot(string key)
        {
            var value = HasUnseen(key);
            if (_lastUnseen.TryGetValue(key, out var previous) && previous == value)
                return false;

            _lastUnseen[key] = value;
            return true;
        }

        private void CaptureSnapshot()
        {
            _lastUnseen.Clear();

            foreach (var key in _sources.Keys)
                _lastUnseen[key] = HasUnseen(key);

            foreach (var key in _flagSources.Keys)
                _lastUnseen[key] = HasUnseen(key);
        }

        private void SetDirty()
        {
            _dirty = true;
            _save.MarkDirty();
        }
    }
}
