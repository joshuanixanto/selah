using System.Text.Json;
using Selah.Models;

namespace Selah.Services;

/// <summary>
/// Offline-first scripture lookup backed by the World English Bible (public domain).
/// This service intentionally does not present WEB text as TB or NIV.
/// </summary>
public sealed class BibleService
{
    private readonly string _dataPath;
    private readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = true };
    private IReadOnlyList<BibleVerse>? _verses;

    public BibleService(string? dataPath = null)
    {
        _dataPath = dataPath ?? Path.Combine(AppContext.BaseDirectory, "Data", "bible-verses.json");
    }

    public async Task<IReadOnlyList<BibleVerse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (_verses is not null) return _verses;

        if (!File.Exists(_dataPath))
        {
            _verses = [];
            return _verses;
        }

        await using var stream = File.OpenRead(_dataPath);
        var entries = await JsonSerializer.DeserializeAsync<List<VerseEntry>>(stream, _options, cancellationToken)
            ?? [];
        _verses = entries.Select(entry => new BibleVerse(
            entry.BookId, entry.Chapter, entry.Verse, entry.Text, BibleTranslation.WorldEnglishBible)).ToArray();
        return _verses;
    }

    public async Task<BibleVerse?> GetRandomAsync(CancellationToken cancellationToken = default)
    {
        var verses = await GetAllAsync(cancellationToken);
        return verses.Count == 0 ? null : verses[Random.Shared.Next(verses.Count)];
    }

    public async Task<IReadOnlyList<BibleVerse>> SearchAsync(
        string query, CancellationToken cancellationToken = default)
    {
        var verses = await GetAllAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(query)) return [];

        var normalized = query.Trim();
        var reference = ReferenceRegex().Match(normalized);
        if (reference.Success)
        {
            var book = reference.Groups[1].Value.Replace(" ", string.Empty).ToLowerInvariant();
            var chapter = int.Parse(reference.Groups[2].Value);
            var verseNumber = reference.Groups[3].Success ? int.Parse(reference.Groups[3].Value) : (int?)null;
            return verses.Where(verse =>
                NormalizeBook(verse.BookId) == book &&
                verse.Chapter == chapter &&
                (verseNumber is null || verse.Verse == verseNumber)).ToArray();
        }

        var terms = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return verses.Where(verse => terms.All(term =>
            verse.Text.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            verse.BookId.Contains(term, StringComparison.OrdinalIgnoreCase))).Take(30).ToArray();
    }

    private static string NormalizeBook(string book) =>
        new string(book.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    [System.Text.RegularExpressions.GeneratedRegex(@"^(.+?)\s+(\d+)(?::(\d+))?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex ReferenceRegex();

    private sealed record VerseEntry(string BookId, int Chapter, int Verse, string Text);
}
