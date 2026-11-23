namespace Selah.Models;

/// <summary>
/// Bible translations supported by Selah's reading experience.
/// The enum identifies a translation; it does not imply that its text is bundled.
/// </summary>
public enum BibleTranslation
{
    TerjemahanBaru,
    NewInternationalVersion
}

/// <summary>
/// Canonical metadata for a Bible book, independent of any translation provider.
/// </summary>
public sealed record BibleBook(
    string Id,
    string Name,
    int TestamentOrder,
    int ChapterCount);

/// <summary>
/// A canonical reference to one chapter in a Bible book.
/// VerseNumber is intentionally omitted because this reference identifies a chapter.
/// </summary>
public sealed record BibleReference(
    string BookId,
    int Chapter,
    BibleTranslation Translation);

/// <summary>
/// A verse returned by an authorized Bible text provider.
/// Text must only be populated from a source whose license permits this use.
/// </summary>
public sealed record BibleVerse(
    string BookId,
    int Chapter,
    int Verse,
    string Text,
    BibleTranslation Translation);
