using Selah.Models;

namespace Selah.Services;

/// <summary>
/// Abstraction over licensed Bible text sources.
/// Implementations can use an official API or another authorized provider without
/// coupling the WPF views to a specific vendor or translation delivery mechanism.
/// </summary>
public interface IBibleProvider
{
    BibleTranslation Translation { get; }

    Task<IReadOnlyList<BibleBook>> GetBooksAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BibleVerse>> GetChapterAsync(
        BibleReference reference,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BibleVerse>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);
}
