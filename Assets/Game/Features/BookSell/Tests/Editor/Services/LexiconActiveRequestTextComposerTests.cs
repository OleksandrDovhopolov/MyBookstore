using System.Collections.Generic;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using Game.Configs.Models;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor.Services
{
    /// <summary>
    /// Covers the composer's contract: an authored override wins, `all` and `none` pick opposite phrase
    /// forms, combos collapse, every condition that decides the outcome is voiced, and the wrapper slots
    /// are stable for a given request id.
    /// </summary>
    public sealed class LexiconActiveRequestTextComposerTests
    {
        private const string Opener = "request.opener.only";
        private const string Lead = "request.lead.only";
        private const string Anchor = "request.anchor.only";

        [Test]
        public void AuthoredOverride_WinsOverLexicon()
        {
            var request = Request("req_override", All(Genre("Fantasy")));
            request.DescriptionKey = "request.req_override.description";

            var loc = Localization().Set(request.DescriptionKey, "Hand-written line.");
            var composer = Composer(loc);

            Assert.AreEqual("Hand-written line.", composer.Compose(request));
        }

        [Test]
        public void AllGroup_UsesPositiveForm()
        {
            var request = Request("req_pos", All(Quality("Gore")));
            var composer = Composer(Localization());

            StringAssert.Contains("nothing squeamish", composer.Compose(request));
        }

        [Test]
        public void NoneGroup_UsesNegativeForm()
        {
            var request = Request("req_neg", All(Genre("Fantasy")));
            request.Conditions.None = new[] { Quality("Gore") };

            var text = Composer(Localization()).Compose(request);

            StringAssert.Contains("but nothing too bloody", text);
            StringAssert.DoesNotContain("nothing squeamish", text);
        }

        [Test]
        public void Combo_CollapsesBothTerms_IntoOnePhrase()
        {
            var request = Request("req_combo", All(Genre("Fantasy"), Quality("Space")));

            var text = Composer(Localization()).Compose(request);

            StringAssert.Contains("proper space sci-fi", text);
            StringAssert.DoesNotContain("a fantasy", text);
            StringAssert.DoesNotContain("set out in space", text);
        }

        [Test]
        public void ContainsAny_ReadsAsOneOrClause()
        {
            var request = Request("req_any", All(new RequestCondition
            {
                Type = "qualities",
                Operator = "containsAny",
                Value = JToken.FromObject(new[] { "Space", "Dry" })
            }));

            StringAssert.Contains(
                "set out in space or dry and matter-of-fact",
                Composer(Localization()).Compose(request));
        }

        /// <summary>
        /// Regression for the removed verbosity buckets, which dropped an open-ended band from the text
        /// while it kept deciding the outcome — req_psycho_01, req_fact_02 and req_kids_01 all shipped
        /// that way. Scoring is all-or-nothing, so a page count the player cannot read is one they lose
        /// to blindly; how many books in the catalogue answer the request never enters into it.
        /// </summary>
        [Test]
        public void NumericBand_IsAlwaysVoiced()
        {
            var request = Request("req_band", All(Genre("Fantasy"), Pages("greater", 500)));

            var text = Composer(Localization()).Compose(request);

            StringAssert.Contains("a fantasy", text);
            StringAssert.Contains("more than 500 pages", text);
        }

        [Test]
        public void SameRequestId_AlwaysPicksTheSameWrapper()
        {
            var request = Request("req_stable", All(Genre("Fantasy")));
            var loc = Localization()
                .Set("request.opener.a", "A!")
                .Set("request.opener.b", "B!")
                .Set("request.opener.c", "C!");

            var phrases = new List<RequestPhraseConfig>(Lexicon())
            {
                Pool("opener.a", "opener", "request.opener.a"),
                Pool("opener.b", "opener", "request.opener.b"),
                Pool("opener.c", "opener", "request.opener.c")
            };

            var first = Composer(loc, phrases).Compose(request);
            var second = Composer(loc, phrases).Compose(request);

            Assert.AreEqual(first, second);
        }

        [Test]
        public void UnknownTerm_IsLeftUnspoken_WithoutBreakingTheSentence()
        {
            var request = Request("req_unknown", All(Genre("Fantasy"), Quality("Whodunnit")));

            var text = Composer(Localization()).Compose(request);

            StringAssert.Contains("a fantasy", text);
            StringAssert.DoesNotContain("Whodunnit", text);
        }

        [Test]
        public void Anchor_UsesTheReferenceBookTitle()
        {
            var request = Request("req_anchor", All(Genre("Fantasy")));
            request.BookTitle = "Anathem";

            StringAssert.Contains("<i>Anathem</i>", Composer(Localization()).Compose(request));
        }

        // ---------- setup ----------

        private static LexiconActiveRequestTextComposer Composer(
            FakeLocalizationService localization,
            IReadOnlyList<RequestPhraseConfig> phrases = null)
        {
            var configs = new FakeConfigsService();
            configs.SetAll(phrases ?? Lexicon());

            return new LexiconActiveRequestTextComposer(configs, localization);
        }

        private static IReadOnlyList<RequestPhraseConfig> Lexicon() => new[]
        {
            Term("genre.fantasy", "genres", "Fantasy", "request.term.genre.fantasy.pos", "request.term.genre.fantasy.neg"),
            Term("quality.gore", "qualities", "Gore", "request.term.quality.gore.pos", "request.term.quality.gore.neg"),
            Term("quality.space", "qualities", "Space", "request.term.quality.space.pos", null),
            Term("quality.dry", "qualities", "Dry", "request.term.quality.dry.pos", null),
            Band("pages.gt500", "pages", "greater", 500, "request.band.pages.gt500"),
            Combo("combo.fantasy_space", "request.combo.fantasy_space", "genre.fantasy", "quality.space"),
            Pool("opener.only", "opener", Opener),
            Pool("lead.only", "lead", Lead),
            Pool("anchor.only", "anchor", Anchor)
        };

        private static FakeLocalizationService Localization() => new FakeLocalizationService()
            .Set("request.term.genre.fantasy.pos", "a fantasy")
            .Set("request.term.genre.fantasy.neg", "but nothing fantastical")
            .Set("request.term.quality.gore.pos", "and don't give me anything squeamish")
            .Set("request.term.quality.gore.neg", "but nothing too bloody")
            .Set("request.term.quality.space.pos", "set out in space")
            .Set("request.term.quality.dry.pos", "dry and matter-of-fact")
            .Set("request.band.pages.gt500", "more than 500 pages")
            .Set("request.combo.fantasy_space", "proper space sci-fi")
            .Set(Opener, "Sooo...")
            .Set(Lead, "I'm after")
            .Set(Anchor, "Something like <i>{0}</i>?");

        // ---------- builders ----------

        private static RequestDefinitionConfig Request(string id, RequestConditionGroup conditions)
            => new() { Id = id, Enabled = true, Conditions = conditions };

        private static RequestConditionGroup All(params RequestCondition[] conditions)
            => new() { All = conditions };

        private static RequestCondition Genre(string value)
            => new() { Type = "genres", Operator = "contains", Value = JToken.FromObject(value) };

        private static RequestCondition Quality(string value)
            => new() { Type = "qualities", Operator = "contains", Value = JToken.FromObject(value) };

        private static RequestCondition Pages(string op, int value)
            => new() { Type = "pages", Operator = op, Value = JToken.FromObject(value) };

        private static RequestPhraseConfig Term(string id, string type, string value, string pos, string neg)
            => new()
            {
                Id = id, Kind = "term", Type = type, Value = JToken.FromObject(value),
                PositiveKey = pos, NegativeKey = neg
            };

        private static RequestPhraseConfig Band(string id, string type, string op, double value, string pos)
            => new()
            {
                Id = id, Kind = "band", Type = type, Operator = op,
                Value = JToken.FromObject(value), PositiveKey = pos
            };

        private static RequestPhraseConfig Combo(string id, string pos, params string[] terms)
            => new() { Id = id, Kind = "combo", Terms = terms, PositiveKey = pos };

        private static RequestPhraseConfig Pool(string id, string kind, string key)
            => new() { Id = id, Kind = kind, PositiveKey = key };
    }
}
