using System;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Book.Sell.Tests.Editor.Services
{
    public sealed class ScriptedPassivePurchaseResolverTests
    {
        private static Customer CustomerWithScript(
            ScriptedPassiveAttempt[] script,
            params string[] genres)
            => new(
                "c1",
                Array.Empty<ICustomerStep>(),
                new CustomerProfile(genres),
                "eddi",
                script != null ? new ScriptedPassivePurchasePlan(script) : null);

        private static CustomerContext Ctx(SalesShelf shelf, ISalesRandom random = null)
            => SalesTestKit.Context(shelf, SalesTestKit.Location(), new RecordingSink(), random: random);

        private static ScriptedPassivePurchaseResolver Resolver(double fallbackChance)
            => new(new RequestedGenrePassiveResolver(new FakeBaseSaleChanceCalculator(fallbackChance)));

        private static ScriptedPassiveAttempt Attempt(string genre, bool forceHit)
            => new(genre, forceHit);

        [Test]
        public void ForceHit_ReturnsBookOfScriptedGenre_EvenWhenFallbackChanceIsZero()
        {
            var resolver = Resolver(fallbackChance: 0d);
            var shelf = SalesTestKit.Shelf(
                SalesTestKit.Book("b_fact", "Fact"),
                SalesTestKit.Book("b_travel", "Travel"));
            var customer = CustomerWithScript(new[] { Attempt("Fact", forceHit: true) }, "Travel");

            var result = resolver.Resolve(customer, Ctx(shelf), shelf.AvailableForSelection());

            Assert.IsTrue(result.Success);
            Assert.AreEqual("Fact", result.ResolvedGenre);
            Assert.AreEqual("b_fact", result.Book.BookId);
        }

        [Test]
        public void ForceMiss_ReturnsMissForScriptedGenre_EvenWhenBookIsStocked()
        {
            var resolver = Resolver(fallbackChance: 1d);
            var shelf = SalesTestKit.Shelf(SalesTestKit.Book("b_travel", "Travel"));
            var customer = CustomerWithScript(new[] { Attempt("Travel", forceHit: false) }, "Travel");

            var result = resolver.Resolve(customer, Ctx(shelf), shelf.AvailableForSelection());

            Assert.IsFalse(result.Success);
            Assert.AreEqual("Travel", result.ResolvedGenre);
            Assert.IsNull(result.Book);
        }

        [Test]
        public void ForceHitWithoutStock_ReturnsMissForScriptedGenre()
        {
            var resolver = Resolver(fallbackChance: 1d);
            var shelf = SalesTestKit.Shelf(SalesTestKit.Book("b_travel", "Travel"));
            var customer = CustomerWithScript(new[] { Attempt("Fact", forceHit: true) }, "Fact");

            LogAssert.Expect(LogType.Error,
                "[Sales.ScriptedPassive] character 'eddi' forced hit for genre 'Fact', but no stock is available.");

            var result = resolver.Resolve(customer, Ctx(shelf), shelf.AvailableForSelection());

            Assert.IsFalse(result.Success);
            Assert.AreEqual("Fact", result.ResolvedGenre);
            Assert.IsNull(result.Book);
        }

        [Test]
        public void ConsumesScriptInOrder_ThenDelegatesToRequestedGenreResolver()
        {
            var resolver = Resolver(fallbackChance: 1d);
            var shelf = SalesTestKit.Shelf(
                SalesTestKit.Book("b_fact", "Fact"),
                SalesTestKit.Book("b_travel", "Travel"));
            var customer = CustomerWithScript(new[]
            {
                Attempt("Fact", forceHit: true),
                Attempt("Travel", forceHit: false)
            }, "Travel");
            var random = new FakeSalesRandom().EnqueueDouble(0d);

            var first = resolver.Resolve(customer, Ctx(shelf, random), shelf.AvailableForSelection());
            var second = resolver.Resolve(customer, Ctx(shelf, random), shelf.AvailableForSelection());
            var fallback = resolver.Resolve(customer, Ctx(shelf, random), shelf.AvailableForSelection());

            Assert.IsTrue(first.Success);
            Assert.AreEqual("Fact", first.ResolvedGenre);
            Assert.AreEqual("b_fact", first.Book.BookId);

            Assert.IsFalse(second.Success);
            Assert.AreEqual("Travel", second.ResolvedGenre);
            Assert.IsNull(second.Book);

            Assert.IsTrue(fallback.Success);
            Assert.AreEqual("Travel", fallback.ResolvedGenre);
            Assert.AreEqual("b_travel", fallback.Book.BookId);
        }
    }
}
