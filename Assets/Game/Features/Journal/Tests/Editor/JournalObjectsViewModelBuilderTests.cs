using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Decor;
using Game.Journal.UI;
using Game.Localization;
using NUnit.Framework;

namespace Game.Journal.UI.Tests.Editor
{
    public sealed class JournalObjectsViewModelBuilderTests
    {
        [SetUp]
        public void SetUp()
        {
            LocalizationLocator.SetService(new FakeLocalization(
                ("decor.d1.name", "Decor One"),
                ("ui.decor.bonus.sale_chance", "sale chance")));
        }

        [TearDown]
        public void TearDown()
        {
            LocalizationLocator.SetService(null);
        }

        [Test]
        public void Build_MapsActiveDecorAndSkipsMissingConfig()
        {
            var configs = new FakeConfigs();
            configs.SetAll(new[] { Decor("d1", "decor.d1.name") });

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
                DisplayNameKey = displayName
            };

        private sealed class FakeLocalization : ILocalizationService
        {
            private readonly Dictionary<string, string> _texts;

            public FakeLocalization(params (string key, string value)[] texts)
            {
                _texts = texts.ToDictionary(
                    text => text.key,
                    text => text.value,
                    StringComparer.Ordinal);
            }

            public string CurrentLocale => "en";
            public event Action<string> LocaleChanged;

            public UniTask WarmupAsync(CancellationToken ct) => UniTask.CompletedTask;

            public string Get(string key)
                => _texts.TryGetValue(key, out var value)
                    ? value
                    : FormatMissingKey(key);

            public string Get(string key, params object[] args)
                => args == null || args.Length == 0
                    ? Get(key)
                    : string.Format(System.Globalization.CultureInfo.InvariantCulture, Get(key), args);

            public bool TryGet(string key, out string value)
                => _texts.TryGetValue(key, out value);

            public void SetLocale(string locale)
                => LocaleChanged?.Invoke(CurrentLocale);

            private static string FormatMissingKey(string key)
                => string.IsNullOrWhiteSpace(key) ? string.Empty : $"[`{key}`]";
        }

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
