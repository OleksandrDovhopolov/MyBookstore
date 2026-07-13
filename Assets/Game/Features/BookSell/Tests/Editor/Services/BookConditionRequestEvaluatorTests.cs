using Book.Sell.Services;
using Game.Configs.Models;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Book.Sell.Tests.Editor.Services
{
    public sealed class BookConditionRequestEvaluatorTests
    {
        private static RequestCondition Condition(string type, string op, object value)
            => new() { Type = type, Operator = op, Value = JToken.FromObject(value) };

        private static RequestDefinitionConfig Request(
            RequestCondition[] all = null,
            RequestCondition[] any = null,
            RequestCondition[] none = null)
            => new()
            {
                Id = "req",
                Enabled = true,
                Conditions = new RequestConditionGroup { All = all, Any = any, None = none }
            };

        private static BookConfig Book()
            => new()
            {
                Id = "book",
                Genres = new[] { "Crime" },
                Qualities = new[] { "Detective", "Series", "Horror" },
                Published = 1893,
                Pages = 189
            };

        [Test]
        public void NullAndEmptyGroups_AreNeutral()
        {
            var evaluator = new BookConditionRequestEvaluator();
            var request = Request(all: new RequestCondition[0], any: null, none: new RequestCondition[0]);

            Assert.IsTrue(evaluator.Evaluate(Book(), request).IsMatch);
        }

        [Test]
        public void GenresAndQualities_AreStrictSeparateFields()
        {
            var evaluator = new BookConditionRequestEvaluator();

            Assert.IsTrue(evaluator.Evaluate(Book(), Request(all: new[]
            {
                Condition("genres", "contains", "Crime"),
                Condition("qualities", "contains", "Detective")
            })).IsMatch);

            Assert.IsFalse(evaluator.Evaluate(Book(), Request(all: new[]
            {
                Condition("genres", "contains", "Detective")
            })).IsMatch, "Detective is a quality, not a genre.");
        }

        [Test]
        public void NumericOperators_WorkForPublishedAndPages()
        {
            var evaluator = new BookConditionRequestEvaluator();
            var request = Request(all: new[]
            {
                Condition("publicationYear", "between", new { min = 1850, max = 1900 }),
                Condition("pages", "lessOrEqual", 200)
            });

            Assert.IsTrue(evaluator.Evaluate(Book(), request).IsMatch);
        }

        [Test]
        public void AnyAndNone_ComposeAsExpected()
        {
            var evaluator = new BookConditionRequestEvaluator();
            var request = Request(
                any: new[]
                {
                    Condition("qualities", "contains", "Romance"),
                    Condition("qualities", "contains", "Horror")
                },
                none: new[]
                {
                    Condition("genres", "contains", "Fantasy")
                });

            Assert.IsTrue(evaluator.Evaluate(Book(), request).IsMatch);
        }

        [Test]
        public void InvalidCondition_IsRejectedAndFailsClosed()
        {
            LogAssert.Expect(UnityEngine.LogType.Error, "[ActiveRequests] request 'req' is invalid: unknown operator 'mysteryOperator'; treated as non-match.");
            var evaluator = new BookConditionRequestEvaluator();
            var request = Request(all: new[]
            {
                Condition("genres", "mysteryOperator", "Crime")
            });

            Assert.IsFalse(evaluator.IsValid(request, out _));
            Assert.IsFalse(evaluator.Evaluate(Book(), request).IsMatch);
        }

        [Test]
        public void DebugText_IsProgrammerReadable()
        {
            var evaluator = new BookConditionRequestEvaluator();
            var request = Request(all: new[] { Condition("pages", "lessOrEqual", 200) });
            request.BookTitle = "A Study in Scarlet";

            StringAssert.Contains("A Study in Scarlet", evaluator.BuildDebugText(request));
            StringAssert.Contains("pages <=", evaluator.BuildDebugText(request));
        }
    }
}
