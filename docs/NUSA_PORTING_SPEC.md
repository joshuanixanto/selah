# Nusa-to-Selah Porting Specification

Reference: https://github.com/Hiroyki/nusa

## Source audit

The Windows desktop build of Nusa is Electron + JavaScript + HTML/CSS. The macOS build is a separate native SwiftUI application. Selah remains native WPF/C#; Nusa is the behavioral and visual reference, not a runtime dependency.

Observed Windows interaction model:
- Fixed transparent 840 x 440 canvas, positioned at the top center and always on top.
- Presentation states include a 150 x 6 px idle lip, a 162 x 24 px hover pill, media/timer/focus previews, transient notification cards, and an approximately 760 px wide expanded panel.
- Hover previews glanceable information; click opens the full panel. Escape or clicking outside closes it. The panel can be pinned.
- Only the notch/panel hit rectangle receives pointer input; the rest of the transparent overlay passes clicks through.
- The renderer reports its active hit rectangle to the host. The host checks cursor position at 25 Hz and toggles mouse-event passthrough.
- Clock, progress, volume and other rapidly changing values update live; the whole view is rebuilt only when presentation or tab changes to avoid flicker.
- Right-click provides lifecycle actions; tray, launch-at-login, and quit are supported.
- The notch yields/fades when a screen-capture or screen-sharing toolbar covers the top-center region.

## Feature parity target

| Nusa capability | Selah target | Current Selah status |
|---|---|---|
| Compact notch, hover preview, expanded panel, pin, outside-click/Escape | Native WPF overlay state machine and adaptive hit testing | Partial shell; transitions exist |
| Home overview | Live media, focus, tasks, weather, battery and calendar cards | Placeholder/partial |
| Now Playing | Windows SMTC metadata, artwork, source app and transport/seek controls | UI shell only |
| Senandung queue/search | Optional player integration isolated from system media | Not implemented |
| Tasks | Natural-language due parsing, quick picks, reminders, snooze/done, persistence | Basic in-memory add only |
| Focus | Configurable focus/break state machine, task-linked focus, daily count | Basic 25-minute timer |
| Prayer and Qibla | Optional local calculation engine, location and reminders | Placeholder |
| Quran | Reader, translations, reciters, bookmarks and verse playback | Placeholder |
| Weather | Open-Meteo current/hourly outlook and rain alerts | Placeholder |
| Tray file shelf | Drag in/out, open/reveal/remove, local path persistence | Placeholder |
| Clipboard Clips | Opt-in local text history, retention and clear controls | Placeholder |
| Themes | Built-in palettes through semantic WPF resources | Implemented; Selah has more presets |
| System | Windows volume, output device, battery and Bluetooth battery peek | Placeholder |
| Calendar | WinRT AppointmentStore integration and permission fallback | Placeholder |
| Camera mirror | Explicit activation; stop when closed or leaving tab | Placeholder |
| Lifecycle | Tray menu, startup, quit and single-instance behavior | Not implemented |

## Target architecture

Keep MainWindow as the composition/orchestration layer. Feature logic belongs in independently testable services and view models.

### Overlay and presentation
- IslandWindowController: position, monitor/DPI changes, topmost behavior and window lifecycle.
- IslandInteractionService: hover/click/Escape/pin/outside-click and hit-test passthrough.
- IslandPresentationState: Lip, Preview, Expanded, Flash and Yielded.
- IslandLayoutMetrics: dimensions, radii, spacing and panel height by presentation/tab.
- IslandAnimationService: coordinated width/height/position/opacity transitions with interruption-safe state changes.
- TransientEventCoordinator: prioritize media, volume, battery, task, focus, prayer and device notifications.

### Feature services
- MediaSessionService and ArtworkService.
- AudioDeviceService and PowerStatusService.
- TaskService, NaturalLanguageTaskParser and ReminderService.
- FocusSessionService.
- WeatherService and LocationService.
- PrayerTimesService, QiblaService and PrayerReminderService.
- QuranContentService and QuranPlaybackService.
- FileShelfService and ClipboardHistoryService.
- CalendarService and CameraPreviewService.
- Existing ThemeService, plus TrayService, StartupService and SettingsStore.

## Persistence and privacy

- Store tasks, focus settings/statistics, theme, selected tab, city preference, shelf paths, bookmarks and preferences locally under %LOCALAPPDATA%/Selah.
- Clipboard capture is opt-in, local-only, bounded by retention/size limits, and has a clear-history action.
- Camera activation requires explicit user interaction and stops when the feature is left.
- Shelf stores file references, not file contents.
- Location supports manual city selection and discloses provider/fallback behavior.
- Do not copy Nusa bundled Quran text, translations, audio, fonts or artwork without checking each asset's separate license. Nusa's code is MIT-licensed, but its README says bundled assets retain their own licenses.

## Implementation sequence

1. Overlay foundation: fixed transparent overlay geometry, notch silhouette, preview/expanded states, adaptive hit testing/click-through, Escape/outside-click, pin, tray and single-instance lifecycle.
2. Visual system: compact tab strip and content composition, responsive panel heights, live indicators, theme tokens and non-flickering updates.
3. Windows core integrations: media session, artwork/source app, audio volume/output device, battery/charging and transient event cards.
4. Productivity: persistent tasks, natural-language due parser, reminders/snooze, focus/break state machine, Clips and file shelf.
5. Context modules: weather/location, prayer/Qibla/reminders, Quran reader/audio/bookmarks, calendar and explicit camera mirror.
6. Hardening: permissions/fallbacks, multi-monitor/DPI, display changes, covered-toolbar yielding, startup/tray lifecycle, tests, logging and performance profiling.

## Engineering constraints

- Do not put Windows API calls, networking, persistence, parsing or timers directly in XAML code-behind.
- Use async APIs with cancellation/timeouts for network and WinRT calls.
- Marshal UI updates through the WPF Dispatcher.
- Keep the overlay non-activating where possible; opening it must not steal foreground focus unless text input is explicitly selected.
- Use observable feature state; do not recreate the whole visual tree on every timer tick.
- Preserve Selah's WPF/C# stack and existing theme service unless a concrete technical reason requires changing them.
- Port behavior and user-facing functionality, not Nusa's Electron source wholesale.