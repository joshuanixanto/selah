using System.Windows.Threading;
using Selah.Models;

namespace Selah.Services;

/// <summary>
/// Raises a daily local-time reminder once per calendar day.
/// The owner must dispose this service when its view model/window is disposed.
/// </summary>
public sealed class ReminderSchedulerService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly ReminderSettings _settings;
    private DateOnly? _lastRaisedDate;
    private bool _disposed;

    public ReminderSchedulerService(ReminderSettings settings, Dispatcher? dispatcher = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _timer = new DispatcherTimer(DispatcherPriority.Background,
            dispatcher ?? Dispatcher.CurrentDispatcher)
        {
            Interval = TimeSpan.FromSeconds(20)
        };
        _timer.Tick += OnTick;
    }

    public event EventHandler? ReminderDue;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_timer.IsEnabled) _timer.Start();
        CheckNow();
    }

    public void CheckNow()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_settings.IsEnabled) return;

        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        if (TimeOnly.FromDateTime(now) < _settings.ReminderTime ||
            _lastRaisedDate == today ||
            _settings.LastReminderDate == today)
            return;

        _lastRaisedDate = today;
        _settings.LastReminderDate = today;
        ReminderDue?.Invoke(this, EventArgs.Empty);
    }

    private void OnTick(object? sender, EventArgs e) => CheckNow();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        ReminderDue = null;
    }
}
