# Selah

A lightweight Windows Dynamic Island companion built with WPF and C#.

Selah takes interaction and feature inspiration from [Nusa](https://github.com/Hiroyki/nusa), while implementing its Windows UI and integrations independently in WPF. Nusa is MIT-licensed; see its repository for the original license and attribution requirements if any source code or assets are reused. Selah does not copy Nusa's bundled media, artwork, fonts, or other assets.

## Current implementation

- Borderless, transparent, always-on-top island window.
- Three island states: a 148×6 px idle notch, a 190×30 px hover pill, and a 760 px-wide expanded panel whose height adapts by section (250–450 px).
- Hover reveals the compact clock pill; clicking the pill opens the panel. Leaving the island returns it to the idle notch. The expanded panel uses a compact horizontal navigation bar, tighter spacing, and section-specific heights.
- Compact horizontal icon navigation for Home, Music, Tasks, Focus, Bible, Weather, Tray, Clips, Calendar, System, Mirror, and Themes, with a Nusa-inspired top-tab layout.
- Local task entry and a 25-minute focus timer.
- Twenty visual theme presets across premium, minimal, cyber/neon, and ambient styles.
- Custom semantic color palette editor with live preview and local JSON persistence.
- Theme presets and custom palettes use shared WPF resource tokens for consistent application-wide updates.

Some sections currently provide their UI shell only. The Bible reader currently includes TB/NIV selection, book selection, chapter input, and search/reading placeholders. Bible text is not bundled; an authorized text provider must be connected before reading, search, bookmarks, or verse-based features can work. `IBibleProvider` defines the integration boundary, while `BibleModels.cs` contains translation and passage models. TB (LAI) and NIV (Biblica) remain subject to their respective licensing terms. Windows media sessions, calendar, audio devices, location, weather, camera, clipboard history, and drag-and-drop tray are also separate integrations and are not represented as completed features yet.

## Requirements

- Windows 10/11 x64
- .NET 10 SDK or newer
- Windows Desktop targeting pack for the target framework

## Run

```powershell
dotnet restore .\src\Selah\Selah.csproj
dotnet run --project .\src\Selah\Selah.csproj
```

## Project structure

- `src/Selah/App.xaml`: shared semantic theme resource tokens.
- `src/Selah/Models/ThemePalette.cs`: strongly typed theme palette model.
- `src/Selah/Services/ThemeService.cs`: preset lookup, resource updates, validation, and custom palette persistence.
- `src/Selah/Models/BibleModels.cs`: Bible translation, book, chapter reference, and verse models.
- `src/Selah/Services/IBibleProvider.cs`: provider contract for authorized Bible text sources.
- `src/Selah/MainWindow.xaml`: island layout, feature views, and theme customizer UI.
- `src/Selah/MainWindow.xaml.cs`: window transitions, navigation, local task interaction, focus timer, and theme UI event handlers.

## Roadmap

Feature integrations should be added as independent services, not embedded in the window code-behind. The next engineering milestone is the window system (multi-monitor/DPI handling, click-through hit testing, tray lifecycle) followed by media-session integration.
