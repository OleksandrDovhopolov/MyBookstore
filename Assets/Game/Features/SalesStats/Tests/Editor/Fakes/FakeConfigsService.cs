using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;

namespace Game.SalesStats.Tests.Editor.Fakes
{
    /// <summary>Minimal in-memory <see cref="IConfigsService"/> for tests — resolves configs by id.</summary>
    public sealed class FakeConfigsService : IConfigsService
    {
        private readonly Dictionary<string, IConfig> _byId = new(StringComparer.OrdinalIgnoreCase);

        public FakeConfigsService Add(IConfig config)
        {
            if (config != null && !string.IsNullOrEmpty(config.Id))
                _byId[config.Id] = config;
            return this;
        }

        public T Get<T>(string id) where T : class, IConfig
            => id != null && _byId.TryGetValue(id, out var cfg) ? cfg as T : null;

        public bool TryGet<T>(string id, out T config) where T : class, IConfig
        {
            config = Get<T>(id);
            return config != null;
        }

        public UniTask<T> GetAsync<T>(string id) where T : class, IConfig => UniTask.FromResult(Get<T>(id));

        public bool IsExists<T>(string id) where T : class, IConfig => Get<T>(id) != null;

        public IReadOnlyList<T> GetAll<T>() where T : class, IConfig
        {
            var list = new List<T>();
            foreach (var cfg in _byId.Values)
                if (cfg is T typed) list.Add(typed);
            return list;
        }

        public UniTask WarmupAsync(CancellationToken ct) => UniTask.CompletedTask;
    }
}
