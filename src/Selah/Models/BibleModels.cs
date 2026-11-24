namespace Selah.Models;

public enum BibleTranslation
{
    TerjemahanBaru,
    NewInternationalVersion,
    WorldEnglishBible
}

public sealed record BibleBook(string Id, string Name, int TestamentOrder, int ChapterCount);

public sealed record BibleReference(string BookId, int Chapter, BibleTranslation Translation);

/// <summary>Scripture text must only come from a provider licensed for this use.</summary>
public sealed record BibleVerse(
    string BookId,
    int Chapter,
    int Verse,
    string Text,
    BibleTranslation Translation);
