using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Book.Sell.Services;
using Game.Configs.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor.Services
{
    public sealed class ConditionActiveRequestGenreResolverTests
    {
        private readonly ConditionActiveRequestGenreResolver _resolver = new();

        [Test]
        public void ResolvesScalarAndArrayPositiveOperators_FromAllAndAny()
        {
            var request = Request(
                all: new[]
                {
                    Condition("genres", "equal", "Classic"),
                    Condition("genres", "contains", "Crime")
                },
                any: new[]
                {
                    Condition("genres", "containsAny", new[] { "Drama", "Fact" }),
                    Condition("genres", "containsAll", new[] { "Fantasy", "Kids" })
                });

            var genres = _resolver.Resolve(request);

            CollectionAssert.AreEqual(
                new[] { "Classic", "Crime", "Drama", "Fact", "Fantasy", "Kids" },
                genres);
        }

        [Test]
        public void DistinctIsCaseInsensitive_AndPreservesFirstCasing()
        {
            var request = Request(
                all: new[]
                {
                    Condition("genres", "equal", "Classic"),
                    Condition("genres", "containsAny", new[] { "classic", "Travel" })
                });

            var genres = _resolver.Resolve(request);

            CollectionAssert.AreEqual(new[] { "Classic", "Travel" }, genres);
        }

        [Test]
        public void IgnoresNoneAndNegativeOperators()
        {
            var request = Request(
                all: new[] { Condition("genres", "notEqual", "Crime") },
                any: new[] { Condition("genres", "contains", "Fact") },
                none: new[] { Condition("genres", "contains", "Travel") });

            var genres = _resolver.Resolve(request);

            CollectionAssert.AreEqual(new[] { "Fact" }, genres);
        }

        [Test]
        public void EmptyGenreSet_WhenNoPositiveGenreConditions()
        {
            var request = Request(
                all: new[]
                {
                    Condition("qualities", "contains", "detective"),
                    Condition("genres", "notContains", "Crime")
                });

            Assert.AreEqual(0, _resolver.Resolve(request).Count);
        }

        [Test]
        public void RealScarletMismatch_ResolvesClassicFromCondition()
        {
            var requests = JsonConvert.DeserializeObject<List<RequestDefinitionConfig>>(
                File.ReadAllText("Assets/Configs/sample_requests.json"));
            var request = requests.First(x => x.Id == "req_scarlet_02");

            var genres = _resolver.Resolve(request);

            CollectionAssert.AreEqual(new[] { "Classic" }, genres);
        }

        private static RequestDefinitionConfig Request(
            string id = "req",
            string genre = null,
            RequestCondition[] all = null,
            RequestCondition[] any = null,
            RequestCondition[] none = null)
            => new()
            {
                Id = id,
                Genre = genre,
                Enabled = true,
                Conditions = new RequestConditionGroup
                {
                    All = all ?? Array.Empty<RequestCondition>(),
                    Any = any ?? Array.Empty<RequestCondition>(),
                    None = none ?? Array.Empty<RequestCondition>()
                }
            };

        private static RequestCondition Condition(string type, string op, object value)
            => new()
            {
                Type = type,
                Operator = op,
                Value = JToken.FromObject(value)
            };
    }
}
