# Selah

A lightweight Windows Dynamic Island companion built with WPF and C#.

Selah takes interaction and feature inspiration from [Nusa](https://github.com/Hiroyki/nusa), while implementing its Windows UI and integrations independently in WPF. Nusa is MIT-licensed; see its repository for the original license and attribution requirements if any source code or assets are reused. Selah does not copy Nusa's bundled media, artwork, fonts, or other assets.

## Current implementation

- Borderless, transparent, always-on-top island window.
- Compact and expanded states with spring-style resize/position transitions.
- Section navigation for Home, Music, Tasks, Focus, Prayer, Quran, Weather, Tray, Clips, Calendar, System, Mirror, and Themes.
- Local task entry and a 25-minute focus timer.
- Twenty visual theme presets across premium, minimal, cyber/neon, and ambient styles.
- Custom semantic color palette editor with live preview and local JSON persistence.
- Theme presets and custom palettes use shared WPF resource tokens for consistent application-wide updates.

Some sections currently provide their UI shell only. Windows media sessions, calendar, audio devices, location, weather, camera, clipboard history, drag-and-drop tray, prayer calculations, and Quran content are separate integrations and are not represented as completed features yet.

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
- `src/Selah/MainWindow.xaml`: island layout, feature views, and theme customizer UI.
- `src/Selah/MainWindow.xaml.cs`: window transitions, navigation, local task interaction, focus timer, and theme UI event handlers.

## Roadmap

Feature integrations should be added as independent services, not embedded in the window code-behind. The next engineering milestone is the window system (multi-monitor/DPI handling, click-through hit testing, tray lifecycle) followed by media-session integration.
