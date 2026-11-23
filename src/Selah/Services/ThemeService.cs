using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Selah.Models;

namespace Selah.Services;

/// <summary>
/// Owns theme presets, applies semantic color resources, and persists the user's custom palette.
/// </summary>
public sealed class ThemeService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Selah",
        "theme.json");

    private readonly IReadOnlyDictionary<string, ThemePalette> _presets =
        new Dictionary<string, ThemePalette>(StringComparer.OrdinalIgnoreCase)
        {
            ["Obsidian"] = Palette("Obsidian", "#101116", "#1B1D27", "#72E4FF", "#A78BFA", "#F4F6FC", "#9BA3B7", "#343746"),
            ["Midnight"] = Palette("Midnight", "#10131F", "#1B2133", "#8EA7FF", "#AAB4CE", "#F2F4FF", "#AAB4CE", "#343D58"),
            ["Tokyo Night"] = Palette("Tokyo Night", "#16161E", "#24283B", "#7AA2F7", "#BB9AF7", "#C0CAF5", "#A9B1D6", "#343B59"),
            ["Nord"] = Palette("Nord", "#2E3440", "#3B4252", "#88C0D0", "#81A1C1", "#ECEFF4", "#D8DEE9", "#4C566A"),
            ["Dracula"] = Palette("Dracula", "#191A21", "#282A36", "#BD93F9", "#FF79C6", "#F8F8F2", "#B8B8C8", "#44475A"),
            ["Gruvbox"] = Palette("Gruvbox", "#1D2021", "#282828", "#FABD2F", "#FE8019", "#EBDBB2", "#BDAE93", "#504945"),
            ["Rosé Pine"] = Palette("Rosé Pine", "#191724", "#26233A", "#C4A7E7", "#EBBCBA", "#E0DEF4", "#908CAA", "#403D52"),
            ["Sunset"] = Palette("Sunset", "#211B25", "#33232D", "#FF9B85", "#F6C177", "#F5D7CE", "#C9A9A5", "#573A43"),
            ["Forest"] = Palette("Forest", "#101D1A", "#1B2B25", "#8CCF9B", "#C4A86A", "#E4F0E6", "#A8C0AD", "#344C3D"),
            ["Ocean"] = Palette("Ocean", "#101C28", "#1A2B3A", "#69C7D2", "#5B9BD5", "#E4F2F7", "#A9C4D0", "#31505D"),
            ["Mono"] = Palette("Mono", "#111111", "#242424", "#E5E5E5", "#A3A3A3", "#F5F5F5", "#B8B8B8", "#414141"),
            ["Cyberpunk"] = Palette("Cyberpunk", "#100B19", "#21142E", "#00F5FF", "#FF3CAC", "#FFF1FC", "#C5A8D2", "#56305F"),
            ["Synthwave"] = Palette("Synthwave", "#17102B", "#281A45", "#FF71CE", "#01CDFE", "#FFF2FF", "#C6B5D8", "#51366D"),
            ["Neon City"] = Palette("Neon City", "#0B1118", "#14212C", "#35F2A5", "#38BDF8", "#E8FFF6", "#9CB8B1", "#294A50"),
            ["Matrix"] = Palette("Matrix", "#050B08", "#0D1B12", "#39FF88", "#B6FFCE", "#E4FFED", "#8BB99A", "#1D4930"),
            ["Tron"] = Palette("Tron", "#06131B", "#0C202C", "#00D9FF", "#4C9DFF", "#E5FAFF", "#8FBAC8", "#15516A"),
            ["Aurora"] = Palette("Aurora", "#111522", "#20283A", "#7CE7C4", "#B69CFF", "#F0F4FF", "#A9B4CA", "#39465B"),
            ["Lavender"] = Palette("Lavender", "#191624", "#282238", "#C4A7E7", "#F0A6CA", "#F4EFFB", "#B8AFC8", "#453A56"),
            ["Arctic"] = Palette("Arctic", "#101923", "#1C2A38", "#8DE3FF", "#B8D7FF", "#F0F8FF", "#AFC4D6", "#354B5D"),
            ["Ember"] = Palette("Ember", "#1A1110", "#2B1B18", "#FF865E", "#FFC46B", "#FFF0E8", "#C9A99A", "#55362E"),
            ["Paper"] = Palette("Paper", "#F4F1EA", "#FFFFFF", "#2563EB", "#7C3AED", "#20242C", "#626B78", "#D8D3C8"),
            ["Snow"] = Palette("Snow", "#F7F9FC", "#FFFFFF", "#2563EB", "#0891B2", "#18212F", "#64748B", "#DCE3EC"),
            ["Solarized Light"] = Palette("Solarized Light", "#FDF6E3", "#EEE8D5", "#268BD2", "#2AA198", "#33424A", "#657B83", "#D6CEB8")
        };

    public IReadOnlyList<string> PresetNames => _presets.Keys.OrderBy(name => name).ToArray();

    public ThemePalette GetPreset(string name) =>
        _presets.TryGetValue(name, out var palette) ? palette : _presets["Obsidian"];

    public void Apply(ThemePalette palette)
    {
        var resources = Application.Current.Resources;
        SetBrush(resources, "IslandBackgroundBrush", palette.Background);
        SetBrush(resources, "CardBackgroundBrush", palette.Surface);
        SetBrush(resources, "AccentBrush", palette.Primary);
        SetBrush(resources, "SecondaryAccentBrush", palette.Secondary);
        // Keep text readable even when a custom palette pairs a light surface with light text.
        var textColor = EnsureContrast(palette.Text, palette.Background, 4.5);
        var mutedColor = EnsureContrast(palette.Muted, palette.Background, 3.0);
        SetBrush(resources, "PrimaryTextBrush", textColor);
        SetBrush(resources, "MutedTextBrush", mutedColor);
        SetBrush(resources, "IslandBorderBrush", palette.Border);
        SetBrush(resources, "SuccessBrush", palette.Success);
        SetBrush(resources, "WarningBrush", palette.Warning);
        SetBrush(resources, "ErrorBrush", palette.Error);
    }

    public ThemePalette? LoadSaved()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return null;
            var json = File.ReadAllText(_settingsPath);
            var palette = JsonSerializer.Deserialize<ThemePalette>(json, JsonOptions);
            return palette is not null && IsValid(palette) ? palette : null;
        }
        catch (IOException) { return null; }
        catch (JsonException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    public bool Save(ThemePalette palette)
    {
        if (!IsValid(palette)) return false;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(palette, JsonOptions));
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    public static bool IsValid(ThemePalette palette) =>
        IsHex(palette.Background) && IsHex(palette.Surface) &&
        IsHex(palette.Primary) && IsHex(palette.Secondary) &&
        IsHex(palette.Text) && IsHex(palette.Muted) &&
        IsHex(palette.Border) && IsHex(palette.Success) &&
        IsHex(palette.Warning) && IsHex(palette.Error);

    private static bool IsHex(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 7 || value[0] != '#') return false;
        return value.Skip(1).All(Uri.IsHexDigit);
    }

    private static string EnsureContrast(string foregroundHex, string backgroundHex, double minimumRatio)
    {
        var foreground = (Color)ColorConverter.ConvertFromString(foregroundHex);
        var background = (Color)ColorConverter.ConvertFromString(backgroundHex);
        if (ContrastRatio(foreground, background) >= minimumRatio)
            return foregroundHex;

        var black = Colors.Black;
        var white = Colors.White;
        return ContrastRatio(black, background) >= ContrastRatio(white, background) ? "#000000" : "#FFFFFF";
    }

    private static double ContrastRatio(Color first, Color second)
    {
        static double Luminance(Color color)
        {
            static double Linear(byte channel)
            {
                var value = channel / 255.0;
                return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        }

        var lighter = Math.Max(Luminance(first), Luminance(second));
        var darker = Math.Min(Luminance(first), Luminance(second));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static void SetBrush(ResourceDictionary resources, string key, string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);

        // XAML resource brushes may be frozen by WPF. Replace the resource
        // instead of mutating an existing SolidColorBrush instance.
        resources[key] = new SolidColorBrush(color);
    }

    private static ThemePalette Palette(
        string name, string background, string surface, string primary,
        string secondary, string text, string muted, string border) =>
        new(name, background, surface, primary, secondary, text, muted, border,
            "#55D6A5", "#F5C76A", "#FF6B78");
}
