using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Book.Sell.Services;
using Game.Configs.Models;
using Newtonsoft.Json;

namespace Book.Sell.Editor
{
    /// <summary>
    /// Checks every active request in sample_requests.json against the whole books.json catalog and reports
    /// the ones no book can ever satisfy. Pure — logs nothing, shows nothing; callers decide how to surface
    /// the report (console + dialog for the menu, thrown exception for the build gate).
    /// <para>
    /// <c>BookConditionRequestEvaluator.IsValid</c> already runs at spawn time, but it only checks
    /// <em>syntax</em> — known type, known operator, well-formed value. A syntactically perfect request that
    /// asks for a genre/quality combination no book carries is accepted and then silently never solvable.
    /// Since scoring is all-or-nothing (<see cref="Book.Sell.API.RecommendationTier"/>), such a request is
    /// dead weight in the pool and blocks any quest counting <c>activePickGenre</c> for that genre.
    /// </para>
    /// <para>
    /// Reads the JSON files directly — there is no <c>IConfigsService</c> outside Play mode — but reuses the
    /// runtime <see cref="BookConditionRequestEvaluator"/> so the verdict cannot drift from actual gameplay.
    /// </para>
    /// </summary>
    public static class ActiveRequestValidator
    {
        public const string BooksPath = "Assets/Configs/books.json";
        public const string RequestsPath = "Assets/Configs/sample_requests.json";

        /// <summary>
        /// How many books the player brings into a day. Mirrors <c>PreparationSessionService.DefaultDailyBookSlots</c>
        /// — duplicated rather than referenced because Book.Sell.Editor must not depend on Game.Preparation
        /// (see docs/ASMDEF_RULES.md §2: features talk through API assemblies only). Keep the two in sync;
        /// the number only feeds the reported hit chance, never the pass/fail verdict.
        /// </summary>
        private const int ShelfCapacity = 30;

        /// <summary>
        /// Solvable "in principle" is not solvable in practice: the request is drawn against a 30-slot shelf,
        /// not against the whole catalogue. At 5 matching books the chance the player even holds an answer is
        /// ~21%, at 15 it is ~50%. Below the error bar the request is a lottery, not a difficulty.
        /// </summary>
        private const int MatchesErrorThreshold = 5;

        private const int MatchesWarningThreshold = 15;

        public static ActiveRequestValidationReport Validate()
        {
            var report = new ActiveRequestValidationReport();

            if (!TryLoad<BookConfig>(BooksPath, report, out var loadedBooks)) return report;
            if (!TryLoad<RequestDefinitionConfig>(RequestsPath, report, out var requests)) return report;

            // Same catalogue the game sees: entries excluded at deserialization can never answer a request,
            // so counting them here would call a starved request solvable (see BookConfig.IsExcludedFromCatalog).
            var books = loadedBooks.Where(book => book != null && !book.IsExcludedFromCatalog).ToList();

            var evaluator = new BookConditionRequestEvaluator();
            var solvableByGenre = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            report.BookCount = books.Count;

            foreach (var request in requests)
            {
                if (request == null || !request.Enabled) continue;
                report.CheckedRequests++;

                // Syntax first: Evaluate() logs its own error for an invalid request, so never reach it here.
                if (!evaluator.IsValid(request, out var reason))
                {
                    report.Errors.Add($"Request '{request.Id}' is malformed and will never spawn: {reason}.");
                    continue;
                }

                var matches = books.Where(book => book != null && evaluator.Evaluate(book, request).IsMatch)
                    .ToList();

                var hitChance = ShelfHitChance(matches.Count, books.Count);
                report.Solvability.Add(new RequestSolvability(request.Id, matches.Count, hitChance));

                if (matches.Count == 0)
                {
                    report.Errors.Add(
                        $"Request '{request.Id}' (authored genre '{request.Genre}') cannot be satisfied by any " +
                        $"of {books.Count} books: {evaluator.BuildDebugText(request)}");
                    continue;
                }

                if (matches.Count < MatchesErrorThreshold)
                {
                    report.Errors.Add(
                        $"Request '{request.Id}' is answerable by only {matches.Count} of {books.Count} books " +
                        $"({hitChance:P0} chance the player holds one on a {ShelfCapacity}-slot shelf). " +
                        $"Below {MatchesErrorThreshold} it plays as a lottery: {evaluator.BuildDebugText(request)}");
                }
                else if (matches.Count < MatchesWarningThreshold)
                {
                    report.Warnings.Add(
                        $"Request '{request.Id}' is answerable by {matches.Count} of {books.Count} books " +
                        $"({hitChance:P0} chance on a {ShelfCapacity}-slot shelf); " +
                        $"{MatchesWarningThreshold} is the comfortable floor.");
                }

                // An activePickGenre task counts the PICKED book's primary genre, so track that — not the
                // request's authoring `genre` field, which evaluation ignores entirely.
                foreach (var book in matches)
                {
                    var primary = book.PrimaryGenre;
                    if (string.IsNullOrEmpty(primary)) continue;

                    if (!solvableByGenre.TryGetValue(primary, out var ids))
                    {
                        ids = new HashSet<string>(StringComparer.Ordinal);
                        solvableByGenre[primary] = ids;
                    }
                    ids.Add(book.Id);
                }
            }

            foreach (var pair in solvableByGenre)
                report.SolvableByPrimaryGenre[pair.Key] = pair.Value.Count;

            AddRequestGenreCoverage(requests, report);
            AddStarvedGenres(books, solvableByGenre, report);
            return report;
        }

        private static void AddRequestGenreCoverage(
            List<RequestDefinitionConfig> requests,
            ActiveRequestValidationReport report)
        {
            var resolver = new ConditionActiveRequestGenreResolver();
            var coveredGenres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var request in requests)
            {
                if (request == null || !request.Enabled) continue;

                var id = string.IsNullOrWhiteSpace(request.Id) ? "<missing-id>" : request.Id;
                var genres = resolver.Resolve(request);
                if (genres == null || genres.Count == 0)
                {
                    report.Warnings.Add($"Request '{id}' has no resolved genre conditions; it can match every customer profile.");
                    continue;
                }

                for (var i = 0; i < genres.Count; i++)
                    coveredGenres.Add(genres[i]);

                if (!string.IsNullOrWhiteSpace(request.Genre) && !ContainsGenre(genres, request.Genre))
                {
                    report.Warnings.Add(
                        $"Request '{id}' metadata genre '{request.Genre}' is not included in resolved genres " +
                        $"[{string.Join(", ", genres)}].");
                }
            }

            foreach (BookGenre genre in Enum.GetValues(typeof(BookGenre)))
            {
                var name = genre.ToString();
                if (!coveredGenres.Contains(name))
                    report.Errors.Add($"Genre '{name}' has no enabled active request; profile-matched spawning will fall back.");
            }
        }

        /// <summary>
        /// Genres whose books can never score Excellent on any request — <c>activePickGenre</c> for them can
        /// never progress, so a quest task using one is unwinnable.
        /// </summary>
        private static void AddStarvedGenres(
            List<BookConfig> books,
            Dictionary<string, HashSet<string>> solvableByGenre,
            ActiveRequestValidationReport report)
        {
            var genres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var book in books)
            {
                var primary = book?.PrimaryGenre;
                if (!string.IsNullOrEmpty(primary)) genres.Add(primary);
            }

            foreach (var genre in genres.OrderBy(g => g, StringComparer.Ordinal))
            {
                if (solvableByGenre.TryGetValue(genre, out var ids) && ids.Count > 0) continue;

                report.StarvedGenres.Add(genre);
                report.Errors.Add(
                    $"No '{genre}' book can score Excellent on any request — a quest task using " +
                    $"activePickGenre '{genre}' would be unwinnable.");
            }
        }

        private static bool TryLoad<T>(string path, ActiveRequestValidationReport report, out List<T> items)
        {
            items = null;

            if (!File.Exists(path))
            {
                report.Errors.Add($"File not found: {path}");
                return false;
            }

            try
            {
                items = JsonConvert.DeserializeObject<List<T>>(File.ReadAllText(path)) ?? new List<T>();
            }
            catch (Exception ex)
            {
                report.Errors.Add($"Failed to parse {path}: {ex.Message}");
                return false;
            }

            if (items.Count != 0) return true;

            report.Errors.Add($"{path} contains no entries.");
            return false;
        }

        /// <summary>
        /// Chance that a shelf of <see cref="ShelfCapacity"/> books drawn from a catalogue of
        /// <paramref name="catalogue"/> holds at least one of the <paramref name="matching"/> answers —
        /// the hypergeometric complement, evaluated as a running product so nothing overflows.
        /// <para>
        /// An upper bound in practice: the shelf is filled from what the player owns, which is a subset
        /// of the catalogue, so the real chance is lower than the number printed here.
        /// </para>
        /// </summary>
        private static double ShelfHitChance(int matching, int catalogue)
        {
            if (matching <= 0 || catalogue <= 0) return 0d;
            if (matching >= catalogue) return 1d;

            var miss = 1d;
            for (var i = 0; i < ShelfCapacity; i++)
            {
                var remaining = catalogue - i;
                if (remaining <= 0) break;

                var misses = catalogue - matching - i;
                if (misses <= 0) return 1d;

                miss *= (double)misses / remaining;
            }

            return 1d - miss;
        }

        private static bool ContainsGenre(IReadOnlyList<string> genres, string expected)
        {
            for (var i = 0; i < genres.Count; i++)
                if (string.Equals(genres[i], expected, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }

    /// <summary>How answerable one request is: matching books in the catalogue and the resulting shelf odds.</summary>
    public readonly struct RequestSolvability
    {
        public RequestSolvability(string requestId, int matches, double shelfHitChance)
        {
            RequestId = requestId;
            Matches = matches;
            ShelfHitChance = shelfHitChance;
        }

        public string RequestId { get; }
        public int Matches { get; }
        public double ShelfHitChance { get; }
    }

    public sealed class ActiveRequestValidationReport
    {
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();
        public List<string> StarvedGenres { get; } = new();
        public List<RequestSolvability> Solvability { get; } = new();
        public Dictionary<string, int> SolvableByPrimaryGenre { get; } = new(StringComparer.Ordinal);

        public int CheckedRequests { get; set; }
        public int BookCount { get; set; }

        public bool HasErrors => Errors.Count > 0;

        public string BuildSummary()
        {
            var coverage = new StringBuilder();
            foreach (var pair in SolvableByPrimaryGenre.OrderBy(p => p.Key, StringComparer.Ordinal))
                coverage.Append(pair.Key).Append('=').Append(pair.Value).Append("  ");

            var sb = new StringBuilder();
            sb.AppendLine($"Checked {CheckedRequests} enabled request(s) against {BookCount} book(s).");
            sb.AppendLine($"Problems: {Errors.Count}");
            sb.AppendLine($"Warnings: {Warnings.Count}");
            if (StarvedGenres.Count > 0)
                sb.AppendLine($"Genres with no winning book: {string.Join(", ", StarvedGenres)}");

            AppendTightestRequests(sb);

            sb.Append("Books that can score Excellent, by primary genre: ");
            sb.Append(coverage.Length > 0 ? coverage.ToString().TrimEnd() : "none");
            return sb.ToString();
        }

        /// <summary>
        /// The tightest requests first — this is the list a designer actually acts on, because the
        /// pass/fail verdict alone does not say how close to the edge the rest of the pool sits.
        /// </summary>
        private void AppendTightestRequests(StringBuilder sb)
        {
            if (Solvability.Count == 0) return;

            sb.AppendLine("Tightest requests (matching books, chance the player holds one):");
            foreach (var entry in Solvability.OrderBy(e => e.Matches).Take(8))
                sb.AppendLine($"  - {entry.RequestId}: {entry.Matches} book(s), {entry.ShelfHitChance:P0}");
        }

        public string FormatErrors()
        {
            var sb = new StringBuilder();
            for (var i = 0; i < Errors.Count; i++)
                sb.AppendLine($"  - {Errors[i]}");
            return sb.ToString();
        }
    }
}
