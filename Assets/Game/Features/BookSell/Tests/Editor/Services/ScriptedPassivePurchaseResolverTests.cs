using System;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using Game.Configs.Models;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Book.Sell.Tests.Editor.Services
{
    public sealed class ScriptedPassivePurchaseResolverTests
    {
        private static Customer CustomerWithScript(string characterId = "eddi", params string[] genres)
            => new("c1", Array.Empty<ICustomerStep>(), new CustomerProfile(genres), characterId);

        private static CustomerContext Ctx(SalesShelf shelf, ISalesRandom random = null)
            => SalesTestKit.Context(shelf, SalesTestKit.Location(), new RecordingSink(), random: random);

        private static ScriptedPassivePurchaseResolver Resolver(
            ScriptedPassivePurchaseConfig[] script,
            double fallbackChance)
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[]
            {
                new CharacterConfig
                {
                    Id = "eddi",
                    ScriptedPassivePurchases = script
                }
            });

            return new ScriptedPassivePurchaseResolver(
                configs,
                new RequestedGenrePassiveResolver(new FakeBaseSaleChanceCalculator(fallbackChance)));
        }

        [Test]
        public void ForceHit_ReturnsBookOfScriptedGenre_EvenWhenFallbackChanceIsZero()
        {
            var resolver = Resolver(new[]
            {
                new ScriptedPassivePurchaseConfig { Genre = "Fact", ForceHit = true }
            }, fallbackChance: 0d);
            var shelf = SalesTestKit.Shelf(
                SalesTestKit.Book("b_fact", "Fact"),
                SalesTestKit.Book("b_travel", "Travel"));

            var result = resolver.Resolve(CustomerWithScript("eddi", "Travel"), Ctx(shelf), shelf.AvailableForSelection());

            Assert.IsTrue(result.Success);
            Assert.AreEqual("Fact", result.ResolvedGenre);
            Assert.AreEqual("b_fact", result.Book.BookId);
        }

        [Test]
        public void ForceMiss_ReturnsMissForScriptedGenre_EvenWhenBookIsStocked()
        {
            var resolver = Resolver(new[]
            {
                new ScriptedPassivePurchaseConfig { Genre = "Travel", ForceHit = false }
            }, fallbackChance: 1d);
            var shelf = SalesTestKit.Shelf(SalesTestKit.Book("b_travel", "Travel"));

            var result = resolver.Resolve(CustomerWithScript("eddi", "Travel"), Ctx(shelf), shelf.AvailableForSelection());

            Assert.IsFalse(result.Success);
            Assert.AreEqual("Travel", result.ResolvedGenre);
            Assert.IsNull(result.Book);
        }

        [Test]
        public void ForceHitWithoutStock_ReturnsMissForScriptedGenre()
        {
            var resolver = Resolver(new[]
            {
                new ScriptedPassivePurchaseConfig { Genre = "Fact", ForceHit = true }
            }, fallbackChance: 1d);
            var shelf = SalesTestKit.Shelf(SalesTestKit.Book("b_travel", "Travel"));

            LogAssert.Expect(LogType.Warning,
                "[Sales.ScriptedPassive] character 'eddi' forced hit for genre 'Fact', but no stock is available.");

            var result = resolver.Resolve(CustomerWithScript("eddi", "Fact"), Ctx(shelf), shelf.AvailableForSelection());

            Assert.IsFalse(result.Success);
            Assert.AreEqual("Fact", result.ResolvedGenre);
            Assert.IsNull(result.Book);
        }

        [Test]
        public void AfterScriptEnds_DelegatesToRequestedGenreResolver()
        {
            var resolver = Resolver(new[]
            {
                new ScriptedPassivePurchaseConfig { Genre = "Fact", ForceHit = false }
            }, fallbackChance: 1d);
            var shelf = SalesTestKit.Shelf(
                SalesTestKit.Book("b_fact", "Fact"),
                SalesTestKit.Book("b_travel", "Travel"));
            var customer = CustomerWithScript("eddi", "Travel");
            var random = new FakeSalesRandom().EnqueueDouble(0d);

            var scripted = resolver.Resolve(customer, Ctx(shelf, random), shelf.AvailableForSelection());
            var fallback = resolver.Resolve(customer, Ctx(shelf, random), shelf.AvailableForSelection());

            Assert.IsFalse(scripted.Success);
            Assert.AreEqual("Fact", scripted.ResolvedGenre);
            Assert.IsTrue(fallback.Success);
            Assert.AreEqual("Travel", fallback.ResolvedGenre);
            Assert.AreEqual("b_travel", fallback.Book.BookId);
        }
    }
}
