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

    private void ExpandButton_Click(object sender, RoutedEventArgs e)
    {
        SetExpanded(true);
    }

    private void CollapseButton_Click(object sender, RoutedEventArgs e)
    {
        SetExpanded(false);
    }

    /// <summary>
    /// Switches the visible content and animates the island to the target size.
    /// </summary>
    private void SetExpanded(bool expanded)
    {
        if (_isExpanded == expanded)
        {
            return;
        }

        _isExpanded = expanded;

        if (expanded)
        {
            CompactContent.Visibility = Visibility.Collapsed;
            ExpandedContent.Visibility = Visibility.Visible;
        }
        else
        {
            ExpandedContent.Visibility = Visibility.Collapsed;
            CompactContent.Visibility = Visibility.Visible;
        }

        var targetWidth = expanded ? ExpandedWidth : CompactWidth;
        var targetHeight = expanded ? ExpandedHeight : CompactHeight;
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        PositionAtTopCenter(targetWidth);

        BeginAnimation(WidthProperty, new DoubleAnimation
        {
            To = targetWidth,
            Duration = TimeSpan.FromMilliseconds(TransitionDurationMs),
            EasingFunction = easing
        });

        BeginAnimation(HeightProperty, new DoubleAnimation
        {
            To = targetHeight,
            Duration = TimeSpan.FromMilliseconds(TransitionDurationMs),
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
