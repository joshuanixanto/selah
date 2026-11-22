# Selah

A Windows Dynamic Island desktop companion built with **WPF and C#**.

> Your desktop, in harmony.

## Technology

- WPF for the desktop UI and window composition
- C# on .NET 10
- XAML for declarative layout and styling

## Requirements

- Windows 10 or Windows 11
- .NET 10 SDK with the Windows Desktop workload/reference packs

## Run

From the repository root:

```powershell
dotnet run --project .\src\Selah\Selah.csproj
```

Or open `Selah.sln` in Visual Studio 2026 (or a compatible Visual Studio version with the .NET 10 SDK).

## Current foundation

- Borderless, transparent, always-on-top island window
- Compact and expanded states
- Basic expand/collapse animation
- Top-center positioning within the primary display work area
- No administrator privileges required

## Planned architecture

The initial window is intentionally small. As features grow, code will move into dedicated layers:

- **Views** — WPF windows and reusable controls
- **ViewModels** — presentation state and commands
- **Services** — Windows integrations such as media, audio, battery, clipboard, and notifications
- **Models** — typed data shared between services and view models
- **Infrastructure** — Win32 interop, settings persistence, and diagnostics

Clipboard monitoring and network-backed features will be opt-in where appropriate.

## License

MIT. See [LICENSE](LICENSE).
