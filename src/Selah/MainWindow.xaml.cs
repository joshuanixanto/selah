using System.Windows;
using System.Windows.Media.Animation;

namespace Selah;

/// <summary>
/// Owns the floating island window and its compact/expanded presentation state.
/// </summary>
public partial class MainWindow : Window
{
    private const double CompactWidth = 360;
    private const double CompactHeight = 64;
    private const double ExpandedWidth = 440;
    private const double ExpandedHeight = 280;
    private const double TopOffset = 18;
    private const int TransitionDurationMs = 220;

    private bool _isExpanded;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PositionAtTopCenter(CompactWidth);
    }

    private void ExpandButton_Click(object sender, RoutedEventArgs e) => SetExpanded(true);

    private void CollapseButton_Click(object sender, RoutedEventArgs e) => SetExpanded(false);

    /// <summary>
    /// Switches the visible content and animates the island while keeping it centered.
    /// </summary>
    private void SetExpanded(bool expanded)
    {
        if (_isExpanded == expanded)
        {
            return;
        }

        _isExpanded = expanded;

        CompactContent.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
        ExpandedContent.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;

        var targetWidth = expanded ? ExpandedWidth : CompactWidth;
        var targetHeight = expanded ? ExpandedHeight : CompactHeight;
        var duration = TimeSpan.FromMilliseconds(TransitionDurationMs);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var workArea = SystemParameters.WorkArea;
        var targetLeft = workArea.Left + (workArea.Width - targetWidth) / 2;

        Top = workArea.Top + TopOffset;

        BeginAnimation(LeftProperty, new DoubleAnimation
        {
            To = targetLeft,
            Duration = duration,
            EasingFunction = easing
        });

        BeginAnimation(WidthProperty, new DoubleAnimation
        {
            To = targetWidth,
            Duration = duration,
            EasingFunction = easing
        });

        BeginAnimation(HeightProperty, new DoubleAnimation
        {
            To = targetHeight,
            Duration = duration,
            EasingFunction = easing
        });
    }

    /// <summary>
    /// Places the island horizontally centered at the top of the primary work area.
    /// </summary>
    private void PositionAtTopCenter(double windowWidth)
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - windowWidth) / 2;
        Top = workArea.Top + TopOffset;
    }
}
