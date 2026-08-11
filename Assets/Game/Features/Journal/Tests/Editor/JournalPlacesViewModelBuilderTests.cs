using System;
using System.Linq;
using Game.Configs.Models;
using Game.Journal.UI;
using NUnit.Framework;

namespace Game.Journal.UI.Tests.Editor
{
    public sealed class JournalPlacesViewModelBuilderTests
    {
        [Test]
        public void Build_MapsUnlockFlag()
        {
            var models = new JournalPlacesViewModelBuilder().Build(
                new[]
                {
                    Location("loc_open", "Open", "Fantasy"),
                    Location("loc_locked", "Locked", "Crime")
                },
                id => id == "loc_open");

            Assert.IsTrue(models.Single(m => m.LocationId == "loc_open").IsUnlocked);
            Assert.IsFalse(models.Single(m => m.LocationId == "loc_locked").IsUnlocked);
        }

        [Test]
        public void Build_NullUnlockPredicate_TreatsAllAsUnlocked()
        {
            var model = new JournalPlacesViewModelBuilder()
                .Build(new[] { Location("loc", "Loc", "Fantasy") }, null)
                .Single();

            Assert.IsTrue(model.IsUnlocked);
        }

        [Test]
        public void Build_DropsInvalidDemandGenres()
        {
            var model = new JournalPlacesViewModelBuilder()
                .Build(new[] { Location("loc", "Loc", "Fantasy", "Mystery", "Crime") }, _ => true)
                .Single();

            CollectionAssert.AreEqual(new[] { "Fantasy", "Crime" }, model.DemandGenres);
        }

        private static LocationConfig Location(string id, string displayName, params string[] demandGenres)
            => new()
            {
                Id = id,
                DisplayName = displayName,
                DemandGenres = demandGenres ?? Array.Empty<string>()
            };
    }
}
