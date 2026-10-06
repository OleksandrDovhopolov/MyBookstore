using System.Collections.Generic;
using System.IO;
using Book.Sell.Editor;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using Game.Configs.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor.Services
{
    /// <summary>
    /// Content guard for CONTENT-2: runs the composer over the live sample_requests.json + request_phrases.json
    /// and asserts every enabled request produces a readable line — not a condition dump, not a raw
    /// localization key. Reads the JSON directly; there is no IConfigsService outside Play mode.
    /// </summary>
    public sealed class ActiveRequestTextContentTests
    {
        private const string PhrasesPath = "Assets/Configs/request_phrases.json";
        private const string LocalizationPath = "Assets/Configs/localization_quests_en.json";

        [Test]
        public void EveryEnabledRequest_ComposesAReadableLine()
        {
            var composer = BuildComposer(out var requests);
            var failures = new List<string>();

            foreach (var request in requests)
            {
                if (request == null || !request.Enabled) continue;

                var text = composer.Compose(request);
                TestContext.WriteLine($"{request.Id,-20} {text}");

                if (string.IsNullOrWhiteSpace(text))
                    failures.Add($"{request.Id}: empty text.");
                else if (text.Contains("ALL:") || text.Contains("NONE:") || text.Contains("ANY:"))
                    failures.Add($"{request.Id}: still shows the condition dump - '{text}'.");
                else if (text.Contains("request."))
                    failures.Add($"{request.Id}: leaked a raw localization key - '{text}'.");
                else if (string.IsNullOrWhiteSpace(request.DescriptionKey) &&
                         !string.IsNullOrWhiteSpace(request.BookTitle) &&
                         !text.Contains(request.BookTitle))
                    failures.Add($"{request.Id}: the reference book is missing from the line - '{text}'.");
            }

            CollectionAssert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void ComposedText_IsStableAcrossCalls()
        {
            var composer = BuildComposer(out var requests);

            foreach (var request in requests)
            {
                if (request == null || !request.Enabled) continue;
                Assert.AreEqual(composer.Compose(request), composer.Compose(request), request.Id);
            }
        }

        private static LexiconActiveRequestTextComposer BuildComposer(out IReadOnlyList<RequestDefinitionConfig> requests)
        {
            var configs = new FakeConfigsService();
            configs.SetAll(Load<RequestPhraseConfig>(PhrasesPath));

            requests = Load<RequestDefinitionConfig>(ActiveRequestValidator.RequestsPath);

            return new LexiconActiveRequestTextComposer(configs, LoadLocalization());
        }

        private static IReadOnlyList<T> Load<T>(string path)
        {
            Assert.IsTrue(File.Exists(path), $"Missing config file: {path}");
            return JsonConvert.DeserializeObject<List<T>>(File.ReadAllText(path));
        }

        private static FakeLocalizationService LoadLocalization()
        {
            Assert.IsTrue(File.Exists(LocalizationPath), $"Missing localization file: {LocalizationPath}");

            var localization = new FakeLocalizationService();
            var table = JObject.Parse(File.ReadAllText(LocalizationPath));
            foreach (var entry in table)
                localization.Set(entry.Key, entry.Value?.ToString() ?? string.Empty);

            return localization;
        }
    }
}
