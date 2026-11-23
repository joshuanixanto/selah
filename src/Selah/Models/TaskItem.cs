using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Selah.Models;

public sealed class TaskItem : INotifyPropertyChanged
{
    private bool _isCompleted;

    public TaskItem(string title)
    {
        Title = title;
    }

    public string Title { get; }

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
