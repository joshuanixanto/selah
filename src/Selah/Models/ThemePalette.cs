namespace Selah.Models;

/// <summary>
/// Semantic colors used by the application theme.
/// Keeping these roles separate lets users change palettes without changing layout.
/// </summary>
public sealed record ThemePalette(
    string Name,
    string Background,
    string Surface,
    string Primary,
    string Secondary,
    string Text,
    string Muted,
    string Border,
    string Success,
    string Warning,
    string Error);
