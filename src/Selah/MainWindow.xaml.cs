using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Selah;

/// <summary>
/// Owns the floating island window, navigation, lightweight local interactions and transitions.
/// Feature integrations are deliberately kept outside this window and will be added as services.
/// </summary>
public partial class MainWindow : Window
{
    private const double CompactWidth = 360;
    private const double CompactHeight = 64;
    private const double ExpandedWidth = 620;
    private const double ExpandedHeight = 590;
    private const double TopOffset = 12;
    private static readonly TimeSpan TransitionDuration = TimeSpan.FromMilliseconds(420);

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
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) => PositionAtTopCenter(CompactWidth);

    private void ExpandButton_Click(object sender, RoutedEventArgs e) => SetExpanded(true);
    private void CollapseButton_Click(object sender, RoutedEventArgs e) => SetExpanded(false);

    private void SetExpanded(bool expanded)
    {
        if (_isExpanded == expanded) return;

        _isExpanded = expanded;
        var targetWidth = expanded ? ExpandedWidth : CompactWidth;
        var targetHeight = expanded ? ExpandedHeight : CompactHeight;
        var workArea = SystemParameters.WorkArea;
        var targetLeft = workArea.Left + (workArea.Width - targetWidth) / 2;

        if (expanded)
        {
            ExpandedContent.Visibility = Visibility.Visible;
            CompactContent.Visibility = Visibility.Collapsed;
            ClockText.Text = DateTime.Now.ToString("ddd, dd MMM  •  HH:mm");
        }

        var animation = new Duration(TransitionDuration);
        var easing = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.18 };

        BeginAnimation(LeftProperty, new DoubleAnimation(Left, targetLeft, animation) { EasingFunction = easing });
        BeginAnimation(WidthProperty, new DoubleAnimation(ActualWidth, targetWidth, animation) { EasingFunction = easing });
        BeginAnimation(HeightProperty, new DoubleAnimation(ActualHeight, targetHeight, animation) { EasingFunction = easing });

        if (!expanded)
        {
            var collapseTimer = new DispatcherTimer { Interval = TransitionDuration };
            collapseTimer.Tick += (_, _) =>
            {
                collapseTimer.Stop();
                CompactContent.Visibility = Visibility.Visible;
                ExpandedContent.Visibility = Visibility.Collapsed;
            };
            collapseTimer.Start();
        }
    }

    private void PositionAtTopCenter(double width)
    {
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - width) / 2;
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
        var name = item.Content?.ToString() ?? "Classic";
        ApplyTheme(name);
    }

    private void ApplyTheme(string name)
    {
        var palette = name switch
        {
            "Midnight" => ("#10131F", "#1B2133", "#8EA7FF", "#AAB4CE", "#343D58"),
            "Tokyo Night" => ("#16161E", "#24283B", "#7AA2F7", "#A9B1D6", "#343B59"),
            "Nord" => ("#2E3440", "#3B4252", "#88C0D0", "#D8DEE9", "#4C566A"),
            "Dracula" => ("#191A21", "#282A36", "#BD93F9", "#F8F8F2", "#44475A"),
            "Gruvbox" => ("#1D2021", "#282828", "#FABD2F", "#EBDBB2", "#504945"),
            "Rosé Pine" => ("#191724", "#26233A", "#C4A7E7", "#E0DEF4", "#403D52"),
            "Sunset" => ("#211B25", "#33232D", "#FF9B85", "#F5D7CE", "#573A43"),
            "Forest" => ("#101D1A", "#1B2B25", "#8CCF9B", "#D1E5D5", "#344C3D"),
            "Ocean" => ("#101C28", "#1A2B3A", "#69C7D2", "#D1E7EC", "#31505D"),
            "Mono" => ("#111111", "#242424", "#E5E5E5", "#D4D4D4", "#414141"),
            _ => ("#171923", "#202431", "#A5B4FC", "#9CA3AF", "#303442")
        };

        SetBrush("IslandBackgroundBrush", palette.Item1);
        SetBrush("CardBackgroundBrush", palette.Item2);
        SetBrush("AccentBrush", palette.Item3);
        SetBrush("MutedTextBrush", palette.Item4);
        SetBrush("IslandBorderBrush", palette.Item5);
    }

    private void SetBrush(string key, string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        if (Application.Current.Resources[key] is SolidColorBrush brush)
            brush.Color = color;
        else
            Application.Current.Resources[key] = new SolidColorBrush(color);
    }
}