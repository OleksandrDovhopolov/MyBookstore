using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Decor;
using Game.Journal.UI;
using NUnit.Framework;

namespace Game.Journal.UI.Tests.Editor
{
    public sealed class JournalObjectsViewModelBuilderTests
    {
        [Test]
        public void Build_MapsActiveDecorAndSkipsMissingConfig()
        {
            var configs = new FakeConfigs();
            configs.SetAll(new[] { Decor("d1", "Decor One") });

            var model = new JournalObjectsViewModelBuilder().Build(
                new[] { "d1", "missing" },
                configs,
                new FakeEffects());

            Assert.AreEqual(1, model.Objects.Count);
            Assert.AreEqual("d1", model.Objects[0].DecorId);
            Assert.AreEqual("Decor One", model.Objects[0].DisplayName);
        }

        [Test]
        public void Build_FormatsBonusRows()
        {
            var model = new JournalObjectsViewModelBuilder().Build(
                Array.Empty<string>(),
                new FakeConfigs(),
                new FakeEffects(
                    new DecorTotalEffect(DecorEffectKind.GenreSaleChance, "Fantasy", 50f),
                    new DecorTotalEffect(DecorEffectKind.CustomerTraffic, null, -10f)));

            CollectionAssert.AreEqual(
                new[] { "+50% Fantasy sale chance", "-10% customers" },
                model.Bonuses.Select(b => b.Label).ToArray());
            Assert.IsTrue(model.Bonuses[0].IsPositive);
            Assert.IsFalse(model.Bonuses[1].IsPositive);
        }

        private static DecorConfig Decor(string id, string displayName)
            => new()
            {
                Id = id,
                DisplayName = displayName
            };

        private sealed class FakeEffects : IDecorTotalEffectsProvider
        {
            private readonly IReadOnlyList<DecorTotalEffect> _effects;

            public FakeEffects(params DecorTotalEffect[] effects)
            {
                _effects = effects ?? Array.Empty<DecorTotalEffect>();
            }

            public IReadOnlyList<DecorTotalEffect> GetTotalEffects(IReadOnlyList<string> activeDecorIds)
                => _effects;
        }

        private sealed class FakeConfigs : IConfigsService
        {
            private readonly Dictionary<Type, IReadOnlyList<IConfig>> _byType = new();

            public void SetAll<T>(IReadOnlyList<T> items) where T : class, IConfig
                => _byType[typeof(T)] = items.Cast<IConfig>().ToArray();

            public UniTask WarmupAsync(CancellationToken ct) => UniTask.CompletedTask;

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
                => TryGet<T>(id, out _);

            public IReadOnlyList<T> GetAll<T>() where T : class, IConfig
                => _byType.TryGetValue(typeof(T), out var items)
                    ? items.Cast<T>().ToArray()
                    : Array.Empty<T>();
        }
    }
}
