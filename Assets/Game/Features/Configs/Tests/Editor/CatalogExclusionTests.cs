using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using NUnit.Framework;

namespace Game.Configs.Tests.Editor
{
    /// <summary>
    /// Books marked "Fake" in the seed sheet must not reach the game. The filter lives in
    /// <see cref="ConfigsService"/> so every read path agrees; these tests pin the predicate
    /// (exact value, case-insensitive, null means "keep") and the interaction with RC overrides.
    /// </summary>
    public sealed class CatalogExclusionTests
    {
        private const string TwoBooks = @"[
  { ""id"": ""book01"", ""genres"": [""Travel""], ""fakeOrReal"": ""Real"" },
  { ""id"": ""book02"", ""genres"": [""Travel""], ""fakeOrReal"": ""Fake"" }
]";

        [Test]
        public void GetAll_DropsFakeBooks_KeepsRealOnes()
        {
            var books = Warmup(TwoBooks).GetAll<BookConfig>();

            Assert.AreEqual(1, books.Count);
            Assert.AreEqual("book01", books[0].Id);
        }

        [Test]
        public void EveryReadPath_AgreesOnTheFilteredCatalog()
        {
            var service = Warmup(TwoBooks);

            Assert.IsTrue(service.IsExists<BookConfig>("book01"));
            Assert.IsNotNull(service.Get<BookConfig>("book01"));

            Assert.IsFalse(service.IsExists<BookConfig>("book02"), "IsExists must not see an excluded book.");
            Assert.IsFalse(service.TryGet<BookConfig>("book02", out var excluded), "TryGet must not resolve an excluded book.");
            Assert.IsNull(excluded);
        }

        [Test]
        public void Exclusion_IgnoresCase()
        {
            var books = Warmup(@"[{ ""id"": ""book01"", ""fakeOrReal"": ""fake"" }]").GetAll<BookConfig>();

            Assert.AreEqual(0, books.Count);
        }

        /// <summary>
        /// Hand-authored books and test fixtures leave the field unset (see SalesTestKit.Book), so the
        /// predicate matches "Fake" outright instead of treating everything that is not "Real" as fake.
        /// </summary>
        [Test]
        public void MissingFakeOrReal_StaysInTheCatalog()
        {
            var books = Warmup(@"[{ ""id"": ""book01"", ""genres"": [""Travel""] }]").GetAll<BookConfig>();

            Assert.AreEqual(1, books.Count);
            Assert.IsNull(books[0].FakeOrReal);
        }

        /// <summary>
        /// The filter runs after the RC merge, so live-ops can bring a book back without a client build.
        /// </summary>
        [Test]
        public void RemoteOverride_CanReturnAnExcludedBookToTheCatalog()
        {
            var overrides = new FakeOverrideSource("books", new Dictionary<string, string>
            {
                ["book02"] = @"{ ""fakeOrReal"": ""Real"" }"
            });

            var books = Warmup(TwoBooks, overrides).GetAll<BookConfig>();

            Assert.AreEqual(2, books.Count);
        }

        /// <summary>And the other way round: an override can take one out.</summary>
        [Test]
        public void RemoteOverride_CanExcludeARealBook()
        {
            var overrides = new FakeOverrideSource("books", new Dictionary<string, string>
            {
                ["book01"] = @"{ ""fakeOrReal"": ""Fake"" }"
            });

            var books = Warmup(TwoBooks, overrides).GetAll<BookConfig>();

            Assert.AreEqual(0, books.Count);
        }

        [Test]
        public void IsFake_MatchesTheExactValueOnly()
        {
            Assert.IsTrue(BookConfig.IsFake("Fake"));
            Assert.IsTrue(BookConfig.IsFake("FAKE"));
            Assert.IsFalse(BookConfig.IsFake("Real"));
            Assert.IsFalse(BookConfig.IsFake(null));
            Assert.IsFalse(BookConfig.IsFake(string.Empty));
        }

        private static ConfigsService Warmup(string booksJson, IConfigOverrideSource overrides = null)
        {
            var service = new ConfigsService(new FakeConfigSource(booksJson), overrides);
            service.WarmupAsync(CancellationToken.None).GetAwaiter().GetResult();
            return service;
        }

        private sealed class FakeConfigSource : IConfigSource
        {
            private readonly string _books;

            public FakeConfigSource(string books) => _books = books;

            public UniTask WarmupAsync(CancellationToken ct) => UniTask.CompletedTask;

            public string GetRaw(string fileName) => fileName == "books" ? _books : null;
        }

        private sealed class FakeOverrideSource : IConfigOverrideSource
        {
            private readonly string _fileName;
            private readonly IReadOnlyDictionary<string, string> _partialsById;

            public FakeOverrideSource(string fileName, IReadOnlyDictionary<string, string> partialsById)
            {
                _fileName = fileName;
                _partialsById = partialsById;
            }

            public bool TryGetOverrides(string fileName, out IReadOnlyDictionary<string, string> partialsById)
            {
                if (fileName == _fileName)
                {
                    partialsById = _partialsById;
                    return true;
                }

                partialsById = null;
                return false;
            }
        }
    }
}
