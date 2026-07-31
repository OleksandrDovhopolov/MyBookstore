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
    /// Checks every active request in sample_requests.json against the whole books_converted.json catalog and reports
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
        public const string BooksPath = "Assets/Configs/books_converted.json";
        public const string RequestsPath = "Assets/Configs/sample_requests.json";

        public static ActiveRequestValidationReport Validate()
        {
            var report = new ActiveRequestValidationReport();

            if (!TryLoad<BookConfig>(BooksPath, report, out var books)) return report;
            if (!TryLoad<RequestDefinitionConfig>(RequestsPath, report, out var requests)) return report;

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

                if (matches.Count == 0)
                {
                    report.Errors.Add(
                        $"Request '{request.Id}' (authored genre '{request.Genre}') cannot be satisfied by any " +
                        $"of {books.Count} books: {evaluator.BuildDebugText(request)}");
                    continue;
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

            AddStarvedGenres(books, solvableByGenre, report);
            return report;
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
    }

    public sealed class ActiveRequestValidationReport
    {
        public List<string> Errors { get; } = new();
        public List<string> StarvedGenres { get; } = new();
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
            if (StarvedGenres.Count > 0)
                sb.AppendLine($"Genres with no winning book: {string.Join(", ", StarvedGenres)}");
            sb.Append("Books that can score Excellent, by primary genre: ");
            sb.Append(coverage.Length > 0 ? coverage.ToString().TrimEnd() : "none");
            return sb.ToString();
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
