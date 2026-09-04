using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using Game.Configs.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Configs.Editor
{
    public static class BooksExcelImporter
    {
        public const string MenuPath = "Tools/Configs/Import Books Excel to Books JSON";
        public const string SheetName = "Books";
        public const string DefaultOutputPath = "Assets/Configs/books.json";
        public const string DefaultLocalizationOutputPath = "Assets/Configs/localization_books_en.json";

        private const string LogPrefix = "[BooksExcelImporter]";

        private static readonly string[] RequiredHeaders =
        {
            "Title",
            "Description",
            "Published",
            "Pages",
            "Author",
            "Genres",
            "Qualities",
            "Fake or Real"
        };

        [MenuItem(MenuPath)]
        public static void ImportFromMenu()
        {
            var path = EditorUtility.OpenFilePanel("Import Books Excel", string.Empty, "xlsx");
            if (string.IsNullOrEmpty(path)) return;

            var result = ConvertWorkbook(path);
            if (!result.Success)
            {
                Debug.LogError($"{LogPrefix} Import failed:\n{string.Join("\n", result.Errors)}");
                return;
            }

            foreach (var warning in result.Warnings)
                Debug.LogWarning($"{LogPrefix} {warning}");

            WriteJson(DefaultOutputPath, result.Books);
            WriteJson(DefaultLocalizationOutputPath, result.Localization);
            Debug.Log($"{LogPrefix} Imported {result.Books.Count} book(s) to {DefaultOutputPath} and {DefaultLocalizationOutputPath}.");
            SyncBundledDefaultsMenu.Sync();
        }

        public static BooksExcelImportResult ConvertWorkbook(string xlsxPath)
        {
            if (string.IsNullOrWhiteSpace(xlsxPath) || !File.Exists(xlsxPath))
                return BooksExcelImportResult.Failed($"Excel file not found: {xlsxPath}");

            try
            {
                return ConvertRows(ReadSheetRows(xlsxPath, SheetName));
            }
            catch (Exception ex)
            {
                return BooksExcelImportResult.Failed(ex.Message);
            }
        }

        public static BooksExcelImportResult ConvertRows(IReadOnlyList<BooksExcelRow> rows)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var books = new JArray();
            var localization = new JObject();
            if (rows == null || rows.Count == 0)
                return BooksExcelImportResult.Failed("Sheet 'Books' contains no data rows.");

            ValidateHeaders(rows, errors);
            if (errors.Count > 0)
                return new BooksExcelImportResult(books, localization, errors, warnings);

            var index = 1;
            foreach (var row in rows)
            {
                var rowErrors = new List<string>();
                var title = ReadRequiredString(row, "Title", rowErrors);
                var description = ReadRequiredString(row, "Description", rowErrors);
                var author = ReadRequiredString(row, "Author", rowErrors);
                var fakeOrReal = ReadRequiredString(row, "Fake or Real", rowErrors);
                var published = ReadRequiredInt(row, "Published", rowErrors);
                var pages = ReadRequiredInt(row, "Pages", rowErrors);
                var genres = ReadGenres(row, rowErrors);
                var qualities = ReadStringList(row, "Qualities", rowErrors);
                var rarityWeight = ReadOptionalNumber(row, rowErrors, "RarityWeight", "Rarity");

                if (rowErrors.Count > 0)
                {
                    warnings.Add(FormatSkippedRowWarning(row, title, rowErrors));
                    index++;
                    continue;
                }

                var id = $"book{index:00}";
                var titleKey = $"book.{id}.title";
                var authorKey = $"book.{id}.author";
                var descriptionKey = $"book.{id}.description";

                books.Add(new JObject
                {
                    ["id"] = id,
                    ["titleKey"] = titleKey,
                    ["authorKey"] = authorKey,
                    ["descriptionKey"] = descriptionKey,
                    ["genres"] = new JArray(genres),
                    ["qualities"] = new JArray(qualities),
                    ["rarityWeight"] = rarityWeight,
                    ["published"] = published,
                    ["pages"] = pages,
                    ["fakeOrReal"] = fakeOrReal
                });

                localization[titleKey] = title;
                localization[authorKey] = author;
                localization[descriptionKey] = description;
                index++;
            }

            return new BooksExcelImportResult(books, localization, errors, warnings);
        }

        private static string FormatSkippedRowWarning(
            BooksExcelRow row,
            string title,
            IReadOnlyList<string> reasons)
        {
            title = string.IsNullOrWhiteSpace(title)
                ? row.Get("Title")?.ToString()?.Trim()
                : title.Trim();
            if (string.IsNullOrEmpty(title))
                title = "<untitled>";

            return $"Row {row.RowNumber} '{title}' skipped: {string.Join("; ", reasons)}";
        }

        private static void WriteJson(string outputPath, JToken json)
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(outputPath, json.ToString(Formatting.Indented));
            AssetDatabase.Refresh();
        }

        private static void ValidateHeaders(IReadOnlyList<BooksExcelRow> rows, List<string> errors)
        {
            var headers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
                foreach (var header in row.Values.Keys)
                    headers.Add(header);

            foreach (var required in RequiredHeaders)
                if (!headers.Contains(required))
                    errors.Add($"Missing required header '{required}'.");
        }

        private static string ReadRequiredString(BooksExcelRow row, string header, List<string> errors)
        {
            var value = row.Get(header)?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(value)) return value;

            errors.Add($"{header} is empty.");
            return null;
        }

        private static int ReadRequiredInt(BooksExcelRow row, string header, List<string> errors)
        {
            var value = row.Get(header);
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                errors.Add($"{header} is empty.");
                return 0;
            }

            if (value is double d) return ReadIntegerNumber(d, header, errors);
            if (value is float f) return ReadIntegerNumber(f, header, errors);
            if (value is int i) return i;
            if (value is long l) return checked((int)l);

            if (double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                return ReadIntegerNumber(parsed, header, errors);

            errors.Add($"{header} is not numeric.");
            return 0;
        }

        private static double ReadOptionalNumber(BooksExcelRow row, List<string> errors, params string[] headers)
        {
            const double defaultValue = 0.5d;
            for (var i = 0; i < headers.Length; i++)
            {
                var header = headers[i];
                var value = row.Get(header);
                if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                    continue;

                if (value is double d) return d;
                if (value is float f) return f;
                if (value is int intValue) return intValue;
                if (value is long longValue) return longValue;

                if (double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                    return parsed;

                errors.Add($"{header} is not numeric.");
                return defaultValue;
            }

            return defaultValue;
        }

        private static int ReadIntegerNumber(double value, string header, List<string> errors)
        {
            var rounded = Math.Round(value);
            if (Math.Abs(value - rounded) < 0.000001d)
                return checked((int)rounded);

            errors.Add($"{header} must be an integer.");
            return 0;
        }

        private static string[] ReadGenres(BooksExcelRow row, List<string> errors)
        {
            var values = ReadStringList(row, "Genres", errors);
            for (var i = 0; i < values.Length; i++)
            {
                if (!BookGenreExtensions.TryParseGenre(values[i], out var genre))
                {
                    errors.Add($"Genres contains unknown value '{values[i]}'.");
                    continue;
                }

                values[i] = genre.ToConfigValue();
            }

            return values;
        }

        private static string[] ReadStringList(BooksExcelRow row, string header, List<string> errors)
        {
            var raw = row.Get(header)?.ToString();
            var values = string.IsNullOrWhiteSpace(raw)
                ? Array.Empty<string>()
                : raw.Split(',')
                    .Select(v => v.Trim())
                    .Where(v => !string.IsNullOrEmpty(v))
                    .ToArray();

            if (values.Length == 0)
                errors.Add($"{header} is empty.");

            return values;
        }

        private static IReadOnlyList<BooksExcelRow> ReadSheetRows(string xlsxPath, string sheetName)
        {
            using var file = File.OpenRead(xlsxPath);
            using var archive = new ZipArchive(file, ZipArchiveMode.Read);
            var sharedStrings = ReadSharedStrings(archive);
            var sheetPath = ResolveSheetPath(archive, sheetName);
            var entry = archive.GetEntry(sheetPath)
                ?? throw new InvalidOperationException($"Worksheet '{sheetName}' not found at '{sheetPath}'.");

            using var stream = entry.Open();
            var doc = XDocument.Load(stream);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var sheetRows = doc.Descendants(ns + "sheetData").Elements(ns + "row").ToArray();
            if (sheetRows.Length == 0)
                return Array.Empty<BooksExcelRow>();

            var headers = ReadCells(sheetRows[0], ns, sharedStrings)
                .ToDictionary(c => c.ColumnIndex, c => c.Value?.ToString()?.Trim() ?? string.Empty);

            var result = new List<BooksExcelRow>();
            foreach (var row in sheetRows.Skip(1))
            {
                var rowNumber = (int?)row.Attribute("r") ?? result.Count + 2;
                var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                var hasValue = false;

                foreach (var cell in ReadCells(row, ns, sharedStrings))
                {
                    if (!headers.TryGetValue(cell.ColumnIndex, out var header) || string.IsNullOrEmpty(header))
                        continue;

                    values[header] = cell.Value;
                    hasValue |= !string.IsNullOrWhiteSpace(cell.Value?.ToString());
                }

                if (hasValue)
                    result.Add(new BooksExcelRow(rowNumber, values));
            }

            return result;
        }

        private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
        {
            var entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null) return Array.Empty<string>();

            using var stream = entry.Open();
            var doc = XDocument.Load(stream);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            return doc.Descendants(ns + "si")
                .Select(si => string.Concat(si.Descendants(ns + "t").Select(t => t.Value)))
                .ToArray();
        }

        private static string ResolveSheetPath(ZipArchive archive, string sheetName)
        {
            var workbook = archive.GetEntry("xl/workbook.xml")
                ?? throw new InvalidOperationException("xl/workbook.xml not found.");
            var rels = archive.GetEntry("xl/_rels/workbook.xml.rels")
                ?? throw new InvalidOperationException("xl/_rels/workbook.xml.rels not found.");

            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace relNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            XNamespace packageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

            using var workbookStream = workbook.Open();
            var workbookDoc = XDocument.Load(workbookStream);
            var sheet = workbookDoc.Descendants(ns + "sheet")
                .FirstOrDefault(s => string.Equals((string)s.Attribute("name"), sheetName, StringComparison.Ordinal));
            if (sheet == null)
                throw new InvalidOperationException($"Worksheet '{sheetName}' not found.");

            var relationshipId = (string)sheet.Attribute(relNs + "id");
            if (string.IsNullOrEmpty(relationshipId))
                throw new InvalidOperationException($"Worksheet '{sheetName}' has no relationship id.");

            using var relsStream = rels.Open();
            var relsDoc = XDocument.Load(relsStream);
            var target = relsDoc.Descendants(packageRelNs + "Relationship")
                .Where(r => string.Equals((string)r.Attribute("Id"), relationshipId, StringComparison.Ordinal))
                .Select(r => (string)r.Attribute("Target"))
                .FirstOrDefault();
            if (string.IsNullOrEmpty(target))
                throw new InvalidOperationException($"Worksheet relationship '{relationshipId}' not found.");

            return NormalizeWorkbookTarget(target);
        }

        private static string NormalizeWorkbookTarget(string target)
        {
            target = target.Replace('\\', '/');
            if (target.StartsWith("/", StringComparison.Ordinal))
                return target.TrimStart('/');
            return "xl/" + target;
        }

        private static IEnumerable<(int ColumnIndex, object Value)> ReadCells(
            XElement row,
            XNamespace ns,
            IReadOnlyList<string> sharedStrings)
        {
            foreach (var cell in row.Elements(ns + "c"))
            {
                var reference = (string)cell.Attribute("r");
                var columnIndex = ColumnIndexFromReference(reference);
                if (columnIndex < 0) continue;

                yield return (columnIndex, ReadCellValue(cell, ns, sharedStrings));
            }
        }

        private static object ReadCellValue(XElement cell, XNamespace ns, IReadOnlyList<string> sharedStrings)
        {
            var type = (string)cell.Attribute("t");
            if (string.Equals(type, "inlineStr", StringComparison.Ordinal))
                return string.Concat(cell.Descendants(ns + "t").Select(t => t.Value));

            var value = cell.Element(ns + "v")?.Value;
            if (string.IsNullOrEmpty(value)) return string.Empty;

            if (string.Equals(type, "s", StringComparison.Ordinal))
            {
                var index = int.Parse(value, CultureInfo.InvariantCulture);
                return index >= 0 && index < sharedStrings.Count ? sharedStrings[index] : string.Empty;
            }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                return number;

            return value;
        }

        private static int ColumnIndexFromReference(string reference)
        {
            if (string.IsNullOrEmpty(reference)) return -1;

            var index = 0;
            var read = false;
            for (var i = 0; i < reference.Length; i++)
            {
                var c = reference[i];
                if (c < 'A' || c > 'Z') break;
                index = index * 26 + c - 'A' + 1;
                read = true;
            }

            return read ? index - 1 : -1;
        }
    }

    public sealed class BooksExcelImportResult
    {
        public BooksExcelImportResult(
            JArray books,
            JObject localization,
            IReadOnlyList<string> errors,
            IReadOnlyList<string> warnings = null)
        {
            Books = books ?? new JArray();
            Localization = localization ?? new JObject();
            Errors = errors ?? Array.Empty<string>();
            Warnings = warnings ?? Array.Empty<string>();
        }

        public JArray Books { get; }
        public JObject Localization { get; }
        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
        public bool Success => Errors.Count == 0;

        public static BooksExcelImportResult Failed(string error)
            => new(new JArray(), new JObject(), new[] { error });
    }

    public sealed class BooksExcelRow
    {
        public BooksExcelRow(int rowNumber, IReadOnlyDictionary<string, object> values)
        {
            RowNumber = rowNumber;
            Values = values ?? new Dictionary<string, object>();
        }

        public int RowNumber { get; }
        public IReadOnlyDictionary<string, object> Values { get; }

        public object Get(string header)
            => Values.TryGetValue(header, out var value) ? value : null;
    }
}
