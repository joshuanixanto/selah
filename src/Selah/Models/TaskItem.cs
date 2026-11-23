using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Selah.Models;

public sealed class TaskItem : INotifyPropertyChanged
{
    private bool _isCompleted;
    private DateTime? _dueAt;

    public TaskItem(string title, DateTime? dueAt = null)
    {
        Title = title;
        _dueAt = dueAt;
    }

    public string Title { get; }

    public DateTime? DueAt
    {
        get => _dueAt;
        set
        {
            if (_dueAt == value) return;
            _dueAt = value;
            OnPropertyChanged();
        }
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        set
        {
            if (_isCompleted == value)
                return;

            _isCompleted = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
