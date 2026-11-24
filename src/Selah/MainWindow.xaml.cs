using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
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
    private const double ExpandedWidth = 800;
    private const double ExpandedHeight = 190;
    private const double TopOffset = 0;
    private static readonly TimeSpan TransitionDuration = TimeSpan.FromMilliseconds(360);

    private readonly ThemeService _themeService = new();
    private readonly TaskService _taskService = new();
    private readonly NaturalLanguageTaskParser _taskParser = new();
    private readonly BibleService _bibleService = new();
    private BibleVerse? _currentBibleVerse;
    private int _bibleStreak;
    private DateOnly? _lastBibleReadDate;
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _focusTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _mediaRefreshTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly MediaPlayerService _mediaPlayer = new();
    private readonly List<FrameworkElement> _pages = [];
    private readonly ObservableCollection<TaskItem> _tasks = [];
    private readonly ObservableCollection<DateTime> _weekDates = [];
    private bool _isExpanded;
    private bool _isHovered;
    private bool _isTransitioning;
    private bool _focusRunning;
    private int _focusRemainingSeconds = 25 * 60;
    private DateTime? _selectedTaskDueAt;
    private bool _isLoadingTasks;

    public MainWindow()
    {
        InitializeComponent();
        _pages.Add(HomePage);
        _pages.Add(MusicPage);
        _pages.Add(TasksPage);
        _pages.Add(FocusPage);
        _pages.Add(BiblePage);
        _pages.Add(WeatherPage);
        _pages.Add(TrayPage);
        _pages.Add(ClipsPage);
        _pages.Add(CalendarPage);
        _pages.Add(SystemPage);
        _pages.Add(MirrorPage);
        _pages.Add(ThemesPage);
        TaskList.ItemsSource = _tasks;
        DashboardTaskList.ItemsSource = _tasks;
        HomeWeekStrip.ItemsSource = _weekDates;
        _tasks.CollectionChanged += Tasks_CollectionChanged;

        _clockTimer.Tick += (_, _) => UpdateClockDisplay();
        _focusTimer.Tick += FocusTimer_Tick;
        _mediaRefreshTimer.Tick += async (_, _) => await RefreshMediaAsync();
        _clockTimer.Start();
        UpdateFocusDisplay();

        var savedPalette = _themeService.LoadSaved();
        var initialPalette = savedPalette ?? _themeService.GetPreset("Obsidian");
        _themeService.Apply(initialPalette);
        PopulatePaletteInputs(initialPalette);
    }


    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Width = IdleWidth;
        Height = IdleHeight;
        IslandSurface.Width = double.NaN;
        IslandSurface.Height = double.NaN;
        PositionAtTopCenter();
        UpdateClockDisplay();
        await LoadTasksAsync();
        await InitializeBibleAsync();
        SetActiveNavigation("Home");
        SetCompactState(false, animate: false);
        await InitializeMediaAsync();
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

    private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isExpanded)
            SetExpanded(false);
        else
            SetCompactState(false);
    }

    private void IslandSurface_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_isExpanded)
        {
            SetExpanded(true);
            e.Handled = true;
            return;
        }

        DependencyObject? current = e.OriginalSource as DependencyObject;
        while (current is not null && !ReferenceEquals(current, IslandSurface))
        {
            if (current is ButtonBase || current is TextBoxBase || current is ComboBox)
                return;
            current = VisualTreeHelper.GetParent(current);
        }

        SetExpanded(false);
        e.Handled = true;
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Escape || !_isExpanded)
            return;

        SetExpanded(false);
        e.Handled = true;
    }

    private void ExpandButton_Click(object sender, RoutedEventArgs e) => SetExpanded(!_isExpanded);

    private void CollapseButton_Click(object sender, RoutedEventArgs e)
    {
        SetExpanded(false);
        SetCompactState(IslandSurface.IsMouseOver);
    }

    private void UpdateClockDisplay()
    {
        var now = DateTime.Now;
        CompactClockText.Text = now.ToString("HH:mm");
        HomeDateText.Text = now.ToString("MMMM yyyy").ToUpperInvariant();
        var monday = now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7));
        _weekDates.Clear();
        for (var day = 0; day < 7; day++)
            _weekDates.Add(monday.AddDays(day));
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
        "Home" => 270,
        "Music" => 205,
        "Tasks" => 285,
        "Focus" => 210,
        "Bible" => 330,
        "Themes" => 350,
        "Weather" => 170,
        "Tray" => 170,
        "Clips" => 170,
        "Calendar" => 170,
        "System" => 170,
        "Mirror" => 170,
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
            button.Padding = isActive ? new Thickness(11, 5, 11, 5) : new Thickness(8, 5, 8, 5);

            button.Background = isActive
                ? (Brush)(TryFindResource("CardBackgroundBrush") ?? Brushes.Transparent)
                : Brushes.Transparent;
            button.Foreground = (Brush)(TryFindResource(isActive ? "PrimaryTextBrush" : "MutedTextBrush")
                ?? Brushes.Gray);
        }
    }

    private void QuickTimer_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string minutesText } ||
            !int.TryParse(minutesText, out var minutes))
            return;

        _focusRemainingSeconds = minutes * 60;
        _focusRunning = true;
        _focusTimer.Start();
        UpdateFocusDisplay();
        FocusStatusText.Text = $"{minutes}-minute focus session in progress.";
        DashboardFocusTimeText.ToolTip = "Focus session running";
    }

    private void TaskSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string schedule })
            return;

        var due = schedule switch
        {
            "30" => DateTime.Now.AddMinutes(30),
            "60" => DateTime.Now.AddHours(1),
            "Tonight" => DateTime.Today.AddHours(20) <= DateTime.Now
                ? DateTime.Today.AddDays(1).AddHours(20)
                : DateTime.Today.AddHours(20),
            "Tomorrow" => DateTime.Today.AddDays(1).AddHours(9),
            _ => DateTime.Now
        };

        _selectedTaskDueAt = due;
        TaskInput.ToolTip = $"Next task due: {due:ddd, HH:mm}";
    }

    private void AddTask_Click(object sender, RoutedEventArgs e)
    {
        AddTask(TaskInput);
    }

    private void DashboardAddTask_Click(object sender, RoutedEventArgs e)
    {
        AddTask(DashboardTaskInput);
    }

    private void AddTask(TextBox input)
    {
        var title = input.Text.Trim();
        if (title.Length == 0)
            return;

        var parsed = _taskParser.Parse(title);
        if (string.IsNullOrWhiteSpace(parsed.Title))
            return;

        var task = new TaskItem(parsed.Title, _selectedTaskDueAt ?? parsed.DueAt);
        _selectedTaskDueAt = null;
        TaskInput.ToolTip = "What needs to get done?";
        task.PropertyChanged += Task_PropertyChanged;
        _tasks.Add(task);
        input.Clear();
        UpdateTaskCount();
        input.Focus();
    }


    private async Task LoadTasksAsync()
    {
        _isLoadingTasks = true;
        try
        {
            var loaded = await _taskService.LoadAsync();
            foreach (var task in _tasks)
                task.PropertyChanged -= Task_PropertyChanged;
            _tasks.Clear();
            foreach (var task in loaded)
            {
                task.PropertyChanged += Task_PropertyChanged;
                _tasks.Add(task);
            }
        }
        catch (Exception ex)
        {
            TaskEmptyText.Text = "Could not load tasks: " + ex.Message;
        }
        finally
        {
            _isLoadingTasks = false;
            UpdateTaskCount();
        }
    }

    private async Task SaveTasksAsync()
    {
        try
        {
            await _taskService.SaveAsync(_tasks);
        }
        catch (Exception ex)
        {
            TaskEmptyText.Text = "Could not save tasks: " + ex.Message;
        }
    }

    private void RemoveTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TaskItem task })
            return;

        task.PropertyChanged -= Task_PropertyChanged;
        _tasks.Remove(task);
        UpdateTaskCount();
    }

    private async void Tasks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateTaskCount();
        if (!_isLoadingTasks)
            await SaveTasksAsync();
    }

    private void Task_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TaskItem.IsCompleted))
        {
            UpdateTaskCount();
            _ = SaveTasksAsync();
        }
    }

    private void UpdateTaskCount()
    {
        var completed = _tasks.Count(task => task.IsCompleted);
        var pending = _tasks.Count - completed;

        TaskCountText.Text = $"{pending} open";
        TaskSummaryText.Text = $"{pending} open · {completed} done";
        HomeTaskCountText.Text = $"{pending} pending";
        DashboardTaskSummaryText.Text = $"{pending} open · {completed} done";
        TaskEmptyText.Visibility = _tasks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FocusStart_Click(object sender, RoutedEventArgs e)
    {
        if (_focusRemainingSeconds <= 0) _focusRemainingSeconds = 25 * 60;
        _focusRunning = true;
        _focusTimer.Start();
        FocusStatusText.Text = "Focus session in progress.";
        DashboardFocusTimeText.ToolTip = "Focus session running";
    }

    private void FocusPause_Click(object sender, RoutedEventArgs e)
    {
        _focusRunning = false;
        _focusTimer.Stop();
        FocusStatusText.Text = "Session paused.";
        DashboardFocusTimeText.ToolTip = "Focus session paused";
    }

    private void FocusReset_Click(object sender, RoutedEventArgs e)
    {
        _focusRunning = false;
        _focusTimer.Stop();
        _focusRemainingSeconds = 25 * 60;
        FocusStatusText.Text = "Ready when you are.";
        DashboardFocusTimeText.ToolTip = "Focus session ready";
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
            DashboardFocusTimeText.ToolTip = "Session complete";
        }
    }

    private void UpdateFocusDisplay()
    {
        var minutes = _focusRemainingSeconds / 60;
        var seconds = _focusRemainingSeconds % 60;
        FocusTimeText.Text = $"{minutes:00}:{seconds:00}";
        DashboardFocusTimeText.Text = FocusTimeText.Text;
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


    private async Task InitializeBibleAsync()
    {
        try
        {
            var verse = await _bibleService.GetRandomAsync();
            if (verse is not null)
                DisplayBibleVerse(verse, "VERSE OF THE DAY");
        }
        catch (Exception ex)
        {
            BibleVerseText.Text = "The offline verse library could not be loaded.";
            BibleVerseReferenceText.Text = ex.Message;
        }
    }

    private void DisplayBibleVerse(BibleVerse verse, string category)
    {
        _currentBibleVerse = verse;
        BibleVerseKindText.Text = category;
        BibleVerseText.Text = verse.Text;
        BibleVerseReferenceText.Text = verse.Reference + "  ·  WEB";
        BibleVerseText.Opacity = 0;
        BibleVerseText.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
    }

    private async void BibleRandom_Click(object sender, RoutedEventArgs e)
    {
        var verse = await _bibleService.GetRandomAsync();
        if (verse is not null)
            DisplayBibleVerse(verse, "RANDOM VERSE");
    }

    private async void BibleSearch_Click(object sender, RoutedEventArgs e)
    {
        var results = await _bibleService.SearchAsync(BibleSearchInput.Text);
        BibleSearchResults.ItemsSource = results;
        if (results.Count == 0)
            BibleCopyStatusText.Text = "No offline match";
        else
            BibleCopyStatusText.Text = $"{results.Count} result(s)";
    }

    private async void BibleSearchInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter) return;
        var results = await _bibleService.SearchAsync(BibleSearchInput.Text);
        BibleSearchResults.ItemsSource = results;
        BibleCopyStatusText.Text = results.Count == 0 ? "No offline match" : $"{results.Count} result(s)";
        e.Handled = true;
    }

    private void BibleSearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BibleSearchResults.SelectedItem is BibleVerse verse)
            DisplayBibleVerse(verse, "SEARCH RESULT");
    }

    private void BibleCopy_Click(object sender, RoutedEventArgs e)
    {
        if (_currentBibleVerse is null) return;
        Clipboard.SetText($"\u201c{_currentBibleVerse.Text}\u201d \u2014 {_currentBibleVerse.Reference} (WEB)");
        BibleCopyStatusText.Text = "Copied";
    }

    private void BibleMarkRead_Click(object sender, RoutedEventArgs e)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (_lastBibleReadDate == today) return;
        _bibleStreak = _lastBibleReadDate == today.AddDays(-1) ? _bibleStreak + 1 : 1;
        _lastBibleReadDate = today;
        BibleStreakText.Text = $"{_bibleStreak}-day streak";
    }

    private async Task InitializeMediaAsync()
    {
        try
        {
            await _mediaPlayer.InitializeAsync();
            await RefreshMediaAsync();
            _mediaRefreshTimer.Start();
        }
        catch (Exception ex)
        {
            PlayerStatusText.Text = "Media service unavailable: " + ex.Message;
        }
    }

    private async Task RefreshMediaAsync()
    {
        try
        {
            var media = await _mediaPlayer.GetCurrentMediaAsync();
            var hasSession = media is not null;

            PlayerTitleText.Text = hasSession ? media!.Title : "Nothing playing";
            PlayerArtistText.Text = hasSession
                ? (string.IsNullOrWhiteSpace(media!.Artist) ? media!.AppName : media!.Artist + " · " + media!.AppName)
                : "Open Spotify, browser, or another media app";
            PlayerStatusText.Text = hasSession ? media!.PlaybackStatus : "Waiting for a media session";
            DashboardNowPlayingTitle.Text = hasSession ? media!.Title : "Nothing playing";
            DashboardNowPlayingArtist.Text = hasSession
                ? (string.IsNullOrWhiteSpace(media!.Artist) ? media!.AppName : media!.Artist)
                : "Start audio on your PC";
            DashboardPlayerStatusText.Text = hasSession ? media!.PlaybackStatus : "Idle";

            var artwork = CreateArtwork(media?.ArtworkBytes);
            MediaArtworkImage.Source = artwork;
            DashboardArtworkImage.Source = artwork;
            DashboardArtworkImage.Visibility = artwork is null ? Visibility.Collapsed : Visibility.Visible;
            MediaArtworkImage.Visibility = artwork is null ? Visibility.Collapsed : Visibility.Visible;

            PreviousTrackButton.IsEnabled = hasSession;
            NextTrackButton.IsEnabled = hasSession;
            DashboardPreviousTrackButton.IsEnabled = hasSession;
            DashboardNextTrackButton.IsEnabled = hasSession;
            DashboardPlayPauseButton.IsEnabled = hasSession;
            PlayPauseButton.IsEnabled = hasSession;
            NextTrackButton.IsEnabled = hasSession;
            PlayPauseButton.Content = hasSession && media!.IsPlaying
                ? ((char)0xE769).ToString()
                : ((char)0xE768).ToString();
            DashboardPlayPauseButton.Content = PlayPauseButton.Content;

            if (hasSession && media!.Duration > TimeSpan.Zero)
            {
                var minimum = Math.Max(0, (media.MinSeekTime - media.StartTime).TotalSeconds);
                var maximum = media.MaxSeekTime > media.StartTime
                    ? (media.MaxSeekTime - media.StartTime).TotalSeconds
                    : media.Duration.TotalSeconds;

                PlayerSeekBar.Minimum = minimum;
                PlayerSeekBar.Maximum = Math.Max(minimum + 0.1, maximum);
                PlayerSeekBar.IsEnabled = maximum > minimum;
                DashboardSeekBar.Minimum = minimum;
                DashboardSeekBar.Maximum = Math.Max(minimum + 0.1, maximum);
                DashboardSeekBar.IsEnabled = maximum > minimum;
                if (!PlayerSeekBar.IsMouseCaptureWithin)
                    PlayerSeekBar.Value = Math.Clamp(media.Position.TotalSeconds, minimum, maximum);
                if (!DashboardSeekBar.IsMouseCaptureWithin)
                    DashboardSeekBar.Value = Math.Clamp(media.Position.TotalSeconds, minimum, maximum);

                PlayerPositionText.Text = FormatMediaTime(media.Position);
                PlayerDurationText.Text = FormatMediaTime(media.Duration);
            }
            else
            {
                PlayerSeekBar.Minimum = 0;
                PlayerSeekBar.Maximum = 100;
                PlayerSeekBar.Value = 0;
                PlayerSeekBar.IsEnabled = false;
                DashboardSeekBar.Minimum = 0;
                DashboardSeekBar.Maximum = 100;
                DashboardSeekBar.Value = 0;
                DashboardSeekBar.IsEnabled = false;
                PlayerPositionText.Text = "0:00";
                PlayerDurationText.Text = "0:00";
            }
        }
        catch (Exception ex)
        {
            PlayerStatusText.Text = "Could not read media session: " + ex.Message;
        }
    }

    private static BitmapImage? CreateArtwork(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
            return null;

        try
        {
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static string FormatMediaTime(TimeSpan value)
    {
        var totalSeconds = Math.Max(0, (long)value.TotalSeconds);
        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds % 3600) / 60;
        var seconds = totalSeconds % 60;

        return hours > 0
            ? $"{hours}:{minutes:00}:{seconds:00}"
            : $"{totalSeconds / 60}:{seconds:00}";
    }

    private async void PlayerSeekBar_Seek(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        await SeekToSelectedPositionAsync(PlayerSeekBar);
    }

    private async void PlayerSeekBar_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        await SeekToSelectedPositionAsync(PlayerSeekBar);
    }

    private async void DashboardSeekBar_Seek(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        await SeekToSelectedPositionAsync(DashboardSeekBar);
    }

    private async Task SeekToSelectedPositionAsync(Slider seekBar)
    {
        if (!seekBar.IsEnabled)
            return;

        await _mediaPlayer.SeekAsync(TimeSpan.FromSeconds(seekBar.Value));
        await RefreshMediaAsync();
    }

    private async void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        await _mediaPlayer.TogglePlayPauseAsync();
        await RefreshMediaAsync();
    }

    private async void PreviousTrack_Click(object sender, RoutedEventArgs e)
    {
        await _mediaPlayer.SkipPreviousAsync();
        await RefreshMediaAsync();
    }

    private async void NextTrack_Click(object sender, RoutedEventArgs e)
    {
        await _mediaPlayer.SkipNextAsync();
        await RefreshMediaAsync();
    }

    protected override void OnClosed(EventArgs e)
    {
        _mediaRefreshTimer.Stop();
        _focusTimer.Stop();
        _clockTimer.Stop();
        _mediaPlayer.Dispose();
        base.OnClosed(e);
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
