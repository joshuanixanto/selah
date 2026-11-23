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
    private const double IdleWidth = 112;
    private const double IdleHeight = 5;
    private const double HoverWidth = 126;
    private const double HoverHeight = 26;
    private const double ExpandedWidth = 760;
    private const double ExpandedHeight = 190;
    private const double TopOffset = 0;
    private static readonly TimeSpan TransitionDuration = TimeSpan.FromMilliseconds(360);

    private readonly ThemeService _themeService = new();
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _focusTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly List<StackPanel> _pages = [];
    private bool _isExpanded;
    private bool _isHovered;
    private bool _isTransitioning;
    private bool _focusRunning;
    private int _focusRemainingSeconds = 25 * 60;
    private int _taskCount;

    public MainWindow()
    {
        InitializeComponent();
        _pages.AddRange(new[]
        {
            HomePage, MusicPage, TasksPage, FocusPage, BiblePage,
            WeatherPage, TrayPage, ClipsPage, CalendarPage, SystemPage, MirrorPage, ThemesPage
        });

        _clockTimer.Tick += (_, _) => UpdateClockDisplay();
        _focusTimer.Tick += FocusTimer_Tick;
        _clockTimer.Start();
        UpdateFocusDisplay();

        var savedPalette = _themeService.LoadSaved();
        var initialPalette = savedPalette ?? _themeService.GetPreset("Obsidian");
        _themeService.Apply(initialPalette);
        PopulatePaletteInputs(initialPalette);
    }


    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Width = IdleWidth;
        Height = IdleHeight;
        IslandSurface.Width = double.NaN;
        IslandSurface.Height = double.NaN;
        PositionAtTopCenter();
        UpdateClockDisplay();
        SetActiveNavigation("Home");
        SetCompactState(false, animate: false);
    }

    private void IslandSurface_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isExpanded)
            SetCompactState(true);
    }

    private void IslandSurface_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isExpanded)
            SetCompactState(false);
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Escape || !_isExpanded)
            return;

        SetExpanded(false);
        e.Handled = true;
    }

    private void ExpandButton_Click(object sender, RoutedEventArgs e) => SetExpanded(true);

    private void CollapseButton_Click(object sender, RoutedEventArgs e)
    {
        SetExpanded(false);
        SetCompactState(IslandSurface.IsMouseOver);
    }

    private void UpdateClockDisplay()
    {
        var now = DateTime.Now;
        CompactClockText.Text = now.ToString("HH:mm");
        HomeDateText.Text = now.ToString("dddd, dd MMMM").ToUpperInvariant();
    }

    /// <summary>
    /// Controls the two compact states: a 6px idle notch and a small hover pill.
    /// The expanded dashboard is deliberately opened only by clicking the pill.
    /// </summary>
    private void SetCompactState(bool hovered, bool animate = true)
    {
        if (_isExpanded) return;

        _isHovered = hovered;
        IslandSurface.CornerRadius = hovered ? new CornerRadius(0, 0, 13, 13) : new CornerRadius(0, 0, 2.5, 2.5);
        CompactContent.Visibility = hovered ? Visibility.Visible : Visibility.Collapsed;
        ExpandedContent.Visibility = Visibility.Collapsed;

        var width = hovered ? HoverWidth : IdleWidth;
        var height = hovered ? HoverHeight : IdleHeight;
        AnimateWindowBounds(width, height, animate);
    }

    private void SetExpanded(bool expanded)
    {
        if (_isExpanded == expanded) return;

        _isExpanded = expanded;
        if (expanded)
        {
            IslandSurface.CornerRadius = new CornerRadius(0, 0, 26, 26);
            CompactContent.Visibility = Visibility.Collapsed;
            ExpandedContent.Visibility = Visibility.Visible;
            UpdateClockDisplay();
            AnimateWindowBounds(ExpandedWidth, GetExpandedHeightForCurrentPage());
            return;
        }

        ExpandedContent.Visibility = Visibility.Collapsed;
        SetCompactState(IslandSurface.IsMouseOver);
    }

    private void AnimateWindowBounds(double targetWidth, double targetHeight, bool animate = true)
    {
        var targetLeft = GetCenteredLeft(targetWidth);
        if (!animate)
        {
            BeginAnimation(WidthProperty, null);
            BeginAnimation(HeightProperty, null);
            BeginAnimation(LeftProperty, null);
            Width = targetWidth;
            Height = targetHeight;
            Left = targetLeft;
            return;
        }

        var currentWidth = ActualWidth;
        var currentHeight = ActualHeight;
        var currentLeft = Left;

        if (_isTransitioning)
        {
            // Freeze the current rendered geometry before replacing an in-flight animation.
            BeginAnimation(WidthProperty, null);
            BeginAnimation(HeightProperty, null);
            BeginAnimation(LeftProperty, null);
            Width = currentWidth;
            Height = currentHeight;
            Left = currentLeft;
        }

        _isTransitioning = true;
        var duration = new Duration(TransitionDuration);
        // BackEase gives the panel a restrained spring-like settle similar to Nusa's CSS spring curve.
        var easing = new BackEase { Amplitude = 0.12, EasingMode = EasingMode.EaseOut };

        var widthAnimation = new DoubleAnimation(Width, targetWidth, duration) { EasingFunction = easing };
        var heightAnimation = new DoubleAnimation(Height, targetHeight, duration) { EasingFunction = easing };
        var leftAnimation = new DoubleAnimation(Left, targetLeft, duration) { EasingFunction = easing };

        heightAnimation.Completed += (_, _) =>
        {
            // Persist final bounds as base values, then release the animation clocks.
            Width = targetWidth;
            Height = targetHeight;
            Left = targetLeft;
            BeginAnimation(WidthProperty, null);
            BeginAnimation(HeightProperty, null);
            BeginAnimation(LeftProperty, null);
            _isTransitioning = false;
        };
        BeginAnimation(WidthProperty, widthAnimation);
        BeginAnimation(HeightProperty, heightAnimation);
        BeginAnimation(LeftProperty, leftAnimation);
    }

    private double GetExpandedHeightForCurrentPage()
    {
        var currentPage = _pages.FirstOrDefault(page => page.Visibility == Visibility.Visible);
        var pageName = currentPage?.Name?.Replace("Page", string.Empty, StringComparison.Ordinal) ?? "Home";
        return GetExpandedHeight(pageName);
    }

    private static double GetExpandedHeight(string pageName) => pageName switch
    {
        "Home" => 190,
        "Music" => 330,
        "Tasks" => 330,
        "Focus" => 250,
        "Bible" => 400,
        "Themes" => 400,
        "Weather" => 190,
        "Tray" => 190,
        "Clips" => 190,
        "Calendar" => 190,
        "System" => 190,
        "Mirror" => 190,
        _ => ExpandedHeight
    };

    private static double GetCenteredLeft(double windowWidth)
    {
        var area = SystemParameters.WorkArea;
        return area.Left + (area.Width - windowWidth) / 2;
    }

    private void PositionAtTopCenter()
    {
        Left = GetCenteredLeft(Width);
        Top = SystemParameters.WorkArea.Top + TopOffset;
    }

    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string pageName }) return;
        var page = _pages.FirstOrDefault(p => p.Name == pageName + "Page");
        if (page is null) return;

        foreach (var item in _pages)
            item.Visibility = ReferenceEquals(item, page) ? Visibility.Visible : Visibility.Collapsed;

        SetActiveNavigation(pageName);
        if (_isExpanded)
            AnimateWindowBounds(ExpandedWidth, GetExpandedHeight(pageName));
    }

    private void SetActiveNavigation(string pageName)
    {
        var tabs = new Dictionary<string, (string Icon, string Label)>(StringComparer.Ordinal)
        {
            ["Home"] = ("⌂", "Home"),
            ["Music"] = ("♫", "Music"),
            ["Tasks"] = ("✓", "Tasks"),
            ["Focus"] = ("◷", "Focus"),
            ["Bible"] = ("▤", "Bible"),
            ["Weather"] = ("☼", "Weather"),
            ["Tray"] = ("▣", "Tray"),
            ["Clips"] = ("▧", "Clips"),
            ["Calendar"] = ("▦", "Calendar"),
            ["System"] = ("⌘", "System"),
            ["Mirror"] = ("◉", "Mirror"),
            ["Themes"] = ("⚙", "Themes")
        };

        foreach (var button in TopNavigation.Children.OfType<Button>())
        {
            var key = button.Tag?.ToString() ?? string.Empty;
            var isActive = string.Equals(key, pageName, StringComparison.Ordinal);
            if (tabs.TryGetValue(key, out var tab))
                button.Content = isActive ? $"{tab.Icon}  {tab.Label}" : tab.Icon;

            button.Background = isActive
                ? new SolidColorBrush(Color.FromArgb(36, 255, 255, 255))
                : Brushes.Transparent;
            button.Foreground = (Brush)(TryFindResource(isActive ? "PrimaryTextBrush" : "MutedTextBrush")
                ?? (isActive ? Brushes.White : Brushes.Gray));
        }
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
