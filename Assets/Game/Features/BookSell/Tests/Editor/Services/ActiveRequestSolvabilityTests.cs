using Book.Sell.Editor;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor
{
    /// <summary>
    /// Regression guard for QUEST_FLOW P1/P2 ("Impossible Kids / Fact Active Requests"). Runs the same
    /// <see cref="ActiveRequestValidator"/> the build gate uses over the live catalog and asserts that no
    /// active request is unsatisfiable and no genre is starved — i.e. every genre has at least one book that
    /// can score Excellent, so an <c>activePickGenre</c> quest task can always progress.
    /// </summary>
    public sealed class ActiveRequestSolvabilityTests
    {
        [Test]
        public void EveryEnabledActiveRequest_IsSolvable_AndNoGenreStarved()
        {
            var report = ActiveRequestValidator.Validate();

            Assert.IsFalse(
                report.HasErrors,
                $"Active-request validation found problems:\n{report.FormatErrors()}\n{report.BuildSummary()}");

            CollectionAssert.IsEmpty(
                report.StarvedGenres,
                $"Genres whose books can never score Excellent (activePickGenre would be unwinnable): " +
                $"{string.Join(", ", report.StarvedGenres)}");
        }

        // Direct P1 (Kids) / P2 (Fact) guard: a book of this primary genre must be able to win some active
        // request, otherwise Tara's / Millie's activePickGenre quest is impossible.
        [TestCase("Kids")]
        [TestCase("Fact")]
        public void Genre_HasAtLeastOneBookThatCanScoreExcellent(string genre)
        {
            var report = ActiveRequestValidator.Validate();

            var solvable = report.SolvableByPrimaryGenre.TryGetValue(genre, out var count) && count > 0;

            Assert.IsTrue(
                solvable,
                $"No '{genre}'-primary book can score Excellent on any active request — " +
                $"activePickGenre '{genre}' quest would be unwinnable (P1/P2 regression).\n{report.BuildSummary()}");
        }
    }
}
