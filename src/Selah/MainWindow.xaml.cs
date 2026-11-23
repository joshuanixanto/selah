using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Selah.Models;
using Selah.Services;

namespace Selah;

/// <summary>
/// Owns the floating island window, navigation, lightweight local interactions and transitions.
/// Feature integrations are deliberately kept outside this window and will be added as services.
/// </summary>
public partial class MainWindow : Window
{
    private const double CompactWidth = 176;
    private const double CompactHeight = 38;
    private const double ExpandedWidth = 620;
    private const double ExpandedHeight = 590;
    private const double TopOffset = 0;
    private static readonly TimeSpan TransitionDuration = TimeSpan.FromMilliseconds(420);

    private readonly ThemeService _themeService = new();
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _focusTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly List<StackPanel> _pages = [];
    private bool _isExpanded;
    private bool _focusRunning;
    private int _focusRemainingSeconds = 25 * 60;
    private int _taskCount;

    public MainWindow()
    {
        InitializeComponent();
        _pages.AddRange(new[]
        {
            HomePage, MusicPage, TasksPage, FocusPage, PrayerPage, QuranPage,
            WeatherPage, TrayPage, ClipsPage, CalendarPage, SystemPage, MirrorPage, ThemesPage
        });

        _clockTimer.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("ddd, dd MMM  •  HH:mm");
        _focusTimer.Tick += FocusTimer_Tick;
        _clockTimer.Start();
        UpdateFocusDisplay();

        var savedPalette = _themeService.LoadSaved();
        var initialPalette = savedPalette ?? _themeService.GetPreset("Obsidian");
        _themeService.Apply(initialPalette);
        PopulatePaletteInputs(initialPalette);
    }

    private bool _isTransitioning;

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Keep the native overlay window fixed; animate only the visible island surface.
        // Width/Height animation preserves the compact pill's typography instead of scaling it down.
        IslandSurface.Width = CompactWidth;
        IslandSurface.Height = CompactHeight;
        PositionAtTopCenter();
    }

    private void ExpandButton_Click(object sender, RoutedEventArgs e) => SetExpanded(true);
    private void CollapseButton_Click(object sender, RoutedEventArgs e) => SetExpanded(false);

    private void SetExpanded(bool expanded)
    {
        if (_isExpanded == expanded || _isTransitioning) return;

        _isExpanded = expanded;
        _isTransitioning = true;

        if (expanded)
        {
            ExpandedContent.Visibility = Visibility.Visible;
            CompactContent.Visibility = Visibility.Collapsed;
            ClockText.Text = DateTime.Now.ToString("ddd, dd MMM  •  HH:mm");
        }

        var targetWidth = expanded ? ExpandedWidth : CompactWidth;
        var targetHeight = expanded ? ExpandedHeight : CompactHeight;
        var duration = new Duration(TransitionDuration);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        var widthAnimation = new DoubleAnimation(IslandSurface.Width, targetWidth, duration)
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.HoldEnd
        };

        var heightAnimation = new DoubleAnimation(IslandSurface.Height, targetHeight, duration)
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.HoldEnd
        };

        heightAnimation.Completed += (_, _) =>
        {
            _isTransitioning = false;

            if (!expanded)
            {
                CompactContent.Visibility = Visibility.Visible;
                ExpandedContent.Visibility = Visibility.Collapsed;
            }
        };

        IslandSurface.BeginAnimation(FrameworkElement.WidthProperty, widthAnimation);
        IslandSurface.BeginAnimation(FrameworkElement.HeightProperty, heightAnimation);
    }

    private void PositionAtTopCenter()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - ExpandedWidth) / 2;
        Top = area.Top + TopOffset;
    }

    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string pageName }) return;
        var page = _pages.FirstOrDefault(p => p.Name == pageName + "Page");
        if (page is null) return;

        foreach (var item in _pages)
            item.Visibility = ReferenceEquals(item, page) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddTask_Click(object sender, RoutedEventArgs e)
    {
        var title = TaskInput.Text.Trim();
        if (title.Length == 0) return;

        TaskList.Items.Add(new ListBoxItem
        {
            Content = title,
            Padding = new Thickness(8, 6, 8, 6),
            Background = new SolidColorBrush(Color.FromRgb(42, 49, 64)),
            Margin = new Thickness(0, 0, 0, 5)
        });
        TaskInput.Clear();
        _taskCount++;
        TaskCountText.Text = $"{_taskCount} pending";
    }

    private void FocusStart_Click(object sender, RoutedEventArgs e)
    {
        if (_focusRemainingSeconds <= 0) _focusRemainingSeconds = 25 * 60;
        _focusRunning = true;
        _focusTimer.Start();
        FocusStatusText.Text = "Focus session in progress.";
    }

    private void FocusPause_Click(object sender, RoutedEventArgs e)
    {
        _focusRunning = false;
        _focusTimer.Stop();
        FocusStatusText.Text = "Session paused.";
    }

    private void FocusReset_Click(object sender, RoutedEventArgs e)
    {
        _focusRunning = false;
        _focusTimer.Stop();
        _focusRemainingSeconds = 25 * 60;
        FocusStatusText.Text = "Ready when you are.";
        UpdateFocusDisplay();
    }

    private void FocusTimer_Tick(object? sender, EventArgs e)
    {
        if (!_focusRunning) return;
        if (_focusRemainingSeconds > 0) _focusRemainingSeconds--;
        UpdateFocusDisplay();

        if (_focusRemainingSeconds == 0)
        {
            _focusRunning = false;
            _focusTimer.Stop();
            FocusStatusText.Text = "Session complete. Take a break.";
        }
    }

    private void UpdateFocusDisplay()
    {
        var minutes = _focusRemainingSeconds / 60;
        var seconds = _focusRemainingSeconds % 60;
        FocusTimeText.Text = $"{minutes:00}:{seconds:00}";
    }

    private void ThemePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || ThemePicker.SelectedItem is not ComboBoxItem item) return;

        var name = item.Content?.ToString() ?? "Obsidian";
        var palette = _themeService.GetPreset(name);
        _themeService.Apply(palette);
        PopulatePaletteInputs(palette);
        ThemeStatusText.Text = $"'{name}' preset applied. Adjust colors or save your own palette.";
    }

    private void ApplyCustomTheme_Click(object sender, RoutedEventArgs e)
    {
        var palette = BuildPaletteFromInputs();
        if (palette is null) return;

        _themeService.Apply(palette);
        ThemeStatusText.Text = "Custom palette preview applied. Save it to keep it after restarting Selah.";
    }

    private void SaveCustomTheme_Click(object sender, RoutedEventArgs e)
    {
        var palette = BuildPaletteFromInputs();
        if (palette is null) return;

        _themeService.Apply(palette);
        if (_themeService.Save(palette))
            ThemeStatusText.Text = "Custom palette saved locally. Selah will restore it on next launch.";
        else
            ThemeStatusText.Text = "Could not save the palette. Check write access to your AppData folder.";
    }

    private ThemePalette? BuildPaletteFromInputs()
    {
        var palette = new ThemePalette(
            "My Custom Palette",
            BackgroundColorInput.Text.Trim(),
            SurfaceColorInput.Text.Trim(),
            PrimaryColorInput.Text.Trim(),
            SecondaryColorInput.Text.Trim(),
            TextColorInput.Text.Trim(),
            MutedColorInput.Text.Trim(),
            BorderColorInput.Text.Trim(),
            "#55D6A5",
            "#F5C76A",
            "#FF6B78");

        if (ThemeService.IsValid(palette)) return palette;

        ThemeStatusText.Text = "Invalid HEX color. Every field must use the #RRGGBB format.";
        return null;
    }

    private void PopulatePaletteInputs(ThemePalette palette)
    {
        BackgroundColorInput.Text = palette.Background;
        SurfaceColorInput.Text = palette.Surface;
        PrimaryColorInput.Text = palette.Primary;
        SecondaryColorInput.Text = palette.Secondary;
        TextColorInput.Text = palette.Text;
        MutedColorInput.Text = palette.Muted;
        BorderColorInput.Text = palette.Border;
    }
}
