using Book.Sell.Domain;
using Book.Sell.Services;
using Game.Configs.Models;

namespace Book.Sell.Tests.Editor.Fakes
{
    /// <summary>Small builders to keep sales tests terse.</summary>
    public static class SalesTestKit
    {
        public static BookConfig Book(string id, string genre = "sci-fi", int price = 80,
            string[] tags = null, string[] mood = null)
            => new()
            {
                Id = id, Title = id, Author = "author", Genre = genre, BasePrice = price,
                Tags = tags ?? new[] { "space" }, Mood = mood ?? new[] { "smart" }
            };

        public static RequestConfig Request(string id, string[] genres = null, int maxPrice = 100,
            RequestDifficulty difficulty = RequestDifficulty.Medium)
            => new()
            {
                Id = id, Text = $"request {id}",
                DesiredGenres = genres ?? new[] { "sci-fi" },
                DesiredTags = new[] { "space" },
                DesiredMood = new[] { "smart" },
                MaxPrice = maxPrice, Difficulty = difficulty, BaseRewardGold = 25
            };

        public static LocationConfig Location(string id = "loc", string[] demandGenres = null, string[] demandTags = null)
            => new()
            {
                Id = id, DisplayName = id,
                DemandGenres = demandGenres ?? new[] { "sci-fi" },
                DemandTags = demandTags ?? new[] { "space" }
            };

        public static SalesShelf Shelf(params BookConfig[] books)
        {
            var shelf = new SalesShelf();
            foreach (var b in books) shelf.Add(new ShelfBook(b));
            return shelf;
        }

        /// <summary>Zero durations + zero spawn interval → one Tick advances each step. Fast & deterministic.</summary>
        public static SalesTuning FastTuning()
            => new()
            {
                ApproachDuration = 0f,
                BrowseDuration = 0f,
                PassiveCommitDelay = 0f,
                SpawnInterval = 0f,
                BaseCustomers = 0
            };

        public static CustomerContext Context(SalesShelf shelf, LocationConfig location, ISalesDaySink sink,
            IInteractionLock interactionLock = null, ISalesRandom random = null, SalesTuning tuning = null)
            => new(
                shelf,
                interactionLock ?? new InteractionLock(),
                random ?? new FakeSalesRandom(),
                new DefaultPassiveSaleSelector(),
                location,
                sink,
                tuning ?? FastTuning());
    }
}
