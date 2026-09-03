using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Localization.Tests.Editor
{
    public sealed class LocalizationServiceTests
    {
        [TearDown]
        public void TearDown()
        {
            LocalizationLocator.SetService(null);
        }

        [Test]
        public void Warmup_MergesDomainFiles()
        {
            var service = Service(
                ("localization_ui_en", @"{""ui.buy"":""Buy""}"),
                ("localization_items_en", @"{""item.apple.name"":""Apple""}"));

            service.WarmupAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual("Buy", service.Get("ui.buy"));
            Assert.AreEqual("Apple", service.Get("item.apple.name"));
        }

        [Test]
        public void MissingKey_ReturnsFormattedKeyAndWarns()
        {
            var service = Service(("localization_ui_en", @"{}"));
            service.WarmupAsync(CancellationToken.None).GetAwaiter().GetResult();

            LogAssert.Expect(UnityEngine.LogType.Warning, "[Localization] Missing key 'ui.missing' for locale 'en'.");

            Assert.AreEqual("[`ui.missing`]", service.Get("ui.missing"));
        }

        [Test]
        public void DuplicateKeyAcrossFiles_LogsErrorAndKeepsFirstValue()
        {
            var service = Service(
                ("localization_ui_en", @"{""shared.key"":""First""}"),
                ("localization_items_en", @"{""shared.key"":""Second""}"));

            LogAssert.Expect(
                UnityEngine.LogType.Error,
                "[Localization] Duplicate key 'shared.key' in 'localization_items_en.json'. Keeping the first value.");

            service.WarmupAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual("First", service.Get("shared.key"));
        }

        [Test]
        public void Get_WithArgs_FormatsInvariantly()
        {
            var service = Service(("localization_ui_en", @"{""ui.day"":""Day {0}""}"));
            service.WarmupAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual("Day 3", service.Get("ui.day", 3));
        }

        [Test]
        public void SetLocale_RebuildsTableAndRaisesEvent()
        {
            var service = Service(
                ("localization_ui_en", @"{""ui.buy"":""Buy""}"),
                ("localization_ui_ru", @"{""ui.buy"":""Buy RU""}"));
            service.WarmupAsync(CancellationToken.None).GetAwaiter().GetResult();

            string raised = null;
            service.LocaleChanged += locale => raised = locale;
            service.SetLocale("ru");

            Assert.AreEqual("ru", raised);
            Assert.AreEqual("Buy RU", service.Get("ui.buy"));
        }

        private static LocalizationService Service(params (string name, string raw)[] files)
            => new(new FakeConfigSource(files));

        private sealed class FakeConfigSource : IConfigSource
        {
            private readonly Dictionary<string, string> _files = new(System.StringComparer.OrdinalIgnoreCase);
            private static readonly string[] Domains = { "ui", "dialogues", "quests", "characters", "items" };
            private static readonly string[] Locales = { "en", "ru" };

            public FakeConfigSource(params (string name, string raw)[] files)
            {
                for (var localeIndex = 0; localeIndex < Locales.Length; localeIndex++)
                {
                    for (var domainIndex = 0; domainIndex < Domains.Length; domainIndex++)
                        _files[$"localization_{Domains[domainIndex]}_{Locales[localeIndex]}"] = "{}";
                }

                for (var i = 0; i < files.Length; i++)
                    _files[files[i].name] = files[i].raw;
            }

            public UniTask WarmupAsync(CancellationToken ct) => UniTask.CompletedTask;

            public string GetRaw(string fileName)
                => _files.TryGetValue(fileName, out var raw) ? raw : null;
        }
    }
}
