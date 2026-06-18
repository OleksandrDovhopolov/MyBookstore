using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;

namespace Game.Shop.Tests.Editor.Fakes
{
    internal sealed class FakeConfigsService : IConfigsService
    {
        private readonly Dictionary<Type, IReadOnlyList<IConfig>> _byType = new();

        public void Seed<T>(IReadOnlyList<T> items) where T : class, IConfig =>
            _byType[typeof(T)] = (IReadOnlyList<IConfig>)items;

        public UniTask WarmupAsync(CancellationToken ct) => UniTask.CompletedTask;

        public IReadOnlyList<T> GetAll<T>() where T : class, IConfig
        {
            if (_byType.TryGetValue(typeof(T), out var raw))
            {
                var typed = new List<T>(raw.Count);
                foreach (var item in raw) typed.Add((T)item);
                return typed;
            }
            return Array.Empty<T>();
        }

        public T Get<T>(string id) where T : class, IConfig
        {
            foreach (var c in GetAll<T>())
                if (string.Equals(c.Id, id, StringComparison.Ordinal)) return c;
            return null;
        }

        public bool TryGet<T>(string id, out T config) where T : class, IConfig
        {
            config = Get<T>(id);
            return config != null;
        }

        public UniTask<T> GetAsync<T>(string id) where T : class, IConfig =>
            UniTask.FromResult(Get<T>(id));

        public bool IsExists<T>(string id) where T : class, IConfig => Get<T>(id) != null;
    }
}
