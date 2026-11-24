using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Selah.Models;

namespace Selah.Services;

/// <summary>
/// Persists task data in the current user's AppData directory.
/// UI controls should call SaveAsync after mutations instead of owning file I/O.
/// </summary>
public sealed class TaskService
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public TaskService(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Selah",
            "tasks.json");
    }

    public async Task<ObservableCollection<TaskItem>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
            return [];

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var records = await JsonSerializer.DeserializeAsync<List<TaskRecord>>(
                stream, _jsonOptions, cancellationToken);
            return new ObservableCollection<TaskItem>(
                (records ?? []).Select(record => new TaskItem(record.Title, record.DueAt)
                {
                    IsCompleted = record.IsCompleted
                }));
        }
        catch (JsonException)
        {
            // Preserve a malformed file for manual recovery rather than silently overwriting it.
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    public async Task SaveAsync(IEnumerable<TaskItem> tasks, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        await _saveLock.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(_filePath)
                ?? throw new InvalidOperationException("Task storage path has no parent directory.");
            Directory.CreateDirectory(directory);

            var records = tasks.Select(task => new TaskRecord(
                task.Title, task.DueAt, task.IsCompleted)).ToList();
            var temporaryPath = _filePath + ".tmp";

            await using (var stream = new FileStream(
                temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None,
                4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, records, _jsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private sealed record TaskRecord(string Title, DateTime? DueAt, bool IsCompleted);
}
