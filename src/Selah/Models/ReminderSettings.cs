namespace Selah.Models;

public sealed class ReminderSettings
{
    public bool IsEnabled { get; set; } = true;
    public TimeOnly ReminderTime { get; set; } = new(7, 30);
    public DateOnly? LastReadDate { get; set; }
    public int CurrentStreak { get; set; }
    public DateOnly? LastReminderDate { get; set; }
}
