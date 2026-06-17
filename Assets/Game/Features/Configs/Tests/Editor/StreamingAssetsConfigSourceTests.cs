using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Game.Configs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Configs.Tests.Editor
{
    public sealed class StreamingAssetsConfigSourceTests
    {
        private string _root;

        [SetUp]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "configs_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
            catch { /* best-effort cleanup */ }
        }

        private string BaseUrl() => "file://" + _root.Replace('\\', '/') + "/";

        private void WriteFile(string name, string content)
            => File.WriteAllText(Path.Combine(_root, name), content);

        [UnityTest]
        public IEnumerator ManifestPresent_LoadsAllListedFiles() => UniTask.ToCoroutine(async () =>
        {
            WriteFile("manifest.json", "[\"books.json\",\"locations.json\"]");
            WriteFile("books.json", "{\"id\":\"book_001\"}");
            WriteFile("locations.json", "{\"id\":\"loc_downtown\"}");

            var source = new StreamingAssetsConfigSource(BaseUrl());
            await source.WarmupAsync(CancellationToken.None);

            Assert.That(source.GetRaw("books"), Is.EqualTo("{\"id\":\"book_001\"}"));
            Assert.That(source.GetRaw("locations"), Is.EqualTo("{\"id\":\"loc_downtown\"}"));
            Assert.That(source.GetRaw("missing"), Is.Null);
        });

        [UnityTest]
        public IEnumerator ManifestMissing_StaysEmptyAndLogsError() => UniTask.ToCoroutine(async () =>
        {
            LogAssert.Expect(LogType.Error, new Regex("StreamingAssetsConfigSource.*Failed to load manifest"));

            var source = new StreamingAssetsConfigSource(BaseUrl());
            await source.WarmupAsync(CancellationToken.None);

            Assert.That(source.GetRaw("books"), Is.Null);
        });

        [UnityTest]
        public IEnumerator EntryMissing_WarnsAndLoadsOthers() => UniTask.ToCoroutine(async () =>
        {
            WriteFile("manifest.json", "[\"books.json\",\"ghost.json\"]");
            WriteFile("books.json", "{\"ok\":true}");

            LogAssert.Expect(LogType.Warning, new Regex("StreamingAssetsConfigSource.*Failed to load.*ghost.json"));

            var source = new StreamingAssetsConfigSource(BaseUrl());
            await source.WarmupAsync(CancellationToken.None);

            Assert.That(source.GetRaw("books"), Is.EqualTo("{\"ok\":true}"));
            Assert.That(source.GetRaw("ghost"), Is.Null);
        });

        [UnityTest]
        public IEnumerator Cancel_ThrowsOperationCanceled() => UniTask.ToCoroutine(async () =>
        {
            WriteFile("manifest.json", "[\"books.json\"]");
            WriteFile("books.json", "{}");

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var thrown = false;
            try
            {
                await new StreamingAssetsConfigSource(BaseUrl()).WarmupAsync(cts.Token);
            }
            catch (OperationCanceledException) { thrown = true; }

            Assert.That(thrown, Is.True, "Expected OperationCanceledException.");
        });
    }
}
