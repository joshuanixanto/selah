using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Selah.Services;

public sealed record MediaSessionSnapshot(
    string Title,
    string Artist,
    string AppName,
    bool IsPlaying,
    string PlaybackStatus,
    byte[]? ArtworkBytes,
    TimeSpan Position,
    TimeSpan Duration,
    TimeSpan StartTime,
    TimeSpan MinSeekTime,
    TimeSpan MaxSeekTime);

public sealed class MediaPlayerService : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private bool _disposed;

    public async Task InitializeAsync()
    {
        _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
    }

    public async Task<MediaSessionSnapshot?> GetCurrentMediaAsync()
    {
        if (_manager is null)
            await InitializeAsync();

        var session = _manager?.GetCurrentSession();
        if (session is null)
            return null;

        var properties = await session.TryGetMediaPropertiesAsync();
        var playback = session.GetPlaybackInfo();
        var timeline = session.GetTimelineProperties();
        var appName = session.SourceAppUserModelId;
        var isPlaying = playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        var status = isPlaying ? "Playing" :
            playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused ? "Paused" : "Not playing";

        var duration = timeline.EndTime > timeline.StartTime
            ? timeline.EndTime - timeline.StartTime
            : TimeSpan.Zero;
        var position = timeline.Position < timeline.StartTime
            ? TimeSpan.Zero
            : timeline.Position - timeline.StartTime;
        if (duration > TimeSpan.Zero && position > duration)
            position = duration;

        return new MediaSessionSnapshot(
            string.IsNullOrWhiteSpace(properties?.Title) ? "Unknown track" : properties.Title,
            properties?.Artist ?? string.Empty,
            string.IsNullOrWhiteSpace(appName) ? "Windows media" : appName,
            isPlaying,
            status,
            await ReadArtworkAsync(properties?.Thumbnail),
            position,
            duration,
            timeline.StartTime,
            timeline.MinSeekTime,
            timeline.MaxSeekTime);
    }

    private static async Task<byte[]?> ReadArtworkAsync(IRandomAccessStreamReference? thumbnail)
    {
        if (thumbnail is null)
            return null;

        try
        {
            using var stream = await thumbnail.OpenReadAsync();
            if (stream.Size == 0 || stream.Size > 10 * 1024 * 1024)
                return null;

            using var reader = new DataReader(stream.GetInputStreamAt(0));
            await reader.LoadAsync((uint)stream.Size);
            var bytes = new byte[(int)stream.Size];
            reader.ReadBytes(bytes);
            return bytes;
        }
        catch
        {
            // Artwork is optional; metadata and playback controls should still work.
            return null;
        }
    }

    public async Task TogglePlayPauseAsync()
    {
        var session = _manager?.GetCurrentSession();
        if (session is not null)
            await session.TryTogglePlayPauseAsync();
    }

    public async Task SkipPreviousAsync()
    {
        var session = _manager?.GetCurrentSession();
        if (session is not null)
            await session.TrySkipPreviousAsync();
    }

    public async Task SkipNextAsync()
    {
        var session = _manager?.GetCurrentSession();
        if (session is not null)
            await session.TrySkipNextAsync();
    }

    public async Task SeekAsync(TimeSpan offsetFromStart)
    {
        var session = _manager?.GetCurrentSession();
        if (session is null)
            return;

        var timeline = session.GetTimelineProperties();
        var target = timeline.StartTime + offsetFromStart;
        if (target < timeline.MinSeekTime)
            target = timeline.MinSeekTime;
        if (target > timeline.MaxSeekTime)
            target = timeline.MaxSeekTime;

        await session.TryChangePlaybackPositionAsync(target.Ticks);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
    }
}
