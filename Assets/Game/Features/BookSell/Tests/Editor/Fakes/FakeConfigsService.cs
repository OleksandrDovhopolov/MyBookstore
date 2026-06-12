using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;

namespace Book.Sell.Tests.Editor.Fakes
{
    /// <summary>Minimal IConfigsService fake for Sales tests: lists are set per type.</summary>
    public sealed class FakeConfigsService : IConfigsService
    {
        private readonly Dictionary<Type, IReadOnlyList<IConfig>> _byType = new();

        public void SetAll<T>(IReadOnlyList<T> items) where T : class, IConfig
            => _byType[typeof(T)] = items.Cast<IConfig>().ToList();

        public IReadOnlyList<T> GetAll<T>() where T : class, IConfig
            => _byType.TryGetValue(typeof(T), out var list) ? list.Cast<T>().ToList() : Array.Empty<T>();

        public T Get<T>(string id) where T : class, IConfig
            => GetAll<T>().FirstOrDefault(c => c.Id == id);

        public bool TryGet<T>(string id, out T config) where T : class, IConfig
        {
            config = Get<T>(id);
            return config != null;
        }

        public UniTask<T> GetAsync<T>(string id) where T : class, IConfig
            => UniTask.FromResult(Get<T>(id));

        public bool IsExists<T>(string id) where T : class, IConfig
            => Get<T>(id) != null;

        public UniTask WarmupAsync(CancellationToken ct) => UniTask.CompletedTask;
    }
}
