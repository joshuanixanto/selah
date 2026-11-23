using Windows.Media.Control;

namespace Selah.Services;

public sealed record MediaSessionSnapshot(string Title, string Artist, string AppName, bool IsPlaying, string PlaybackStatus);

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
        if (_manager is null) await InitializeAsync();
        var session = _manager?.GetCurrentSession();
        if (session is null) return null;

        var properties = await session.TryGetMediaPropertiesAsync();
        var playback = session.GetPlaybackInfo();
        var appName = session.SourceAppUserModelId;
        var isPlaying = playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        var status = isPlaying ? "Playing" :
            playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused ? "Paused" : "Not playing";

        return new MediaSessionSnapshot(
            string.IsNullOrWhiteSpace(properties?.Title) ? "Unknown track" : properties.Title,
            properties?.Artist ?? string.Empty,
            string.IsNullOrWhiteSpace(appName) ? "Windows media" : appName,
            isPlaying,
            status);
    }

    public async Task TogglePlayPauseAsync()
    {
        var session = _manager?.GetCurrentSession();
        if (session is not null) await session.TryTogglePlayPauseAsync();
    }

    public async Task SkipPreviousAsync()
    {
        var session = _manager?.GetCurrentSession();
        if (session is not null) await session.TrySkipPreviousAsync();
    }

    public async Task SkipNextAsync()
    {
        var session = _manager?.GetCurrentSession();
        if (session is not null) await session.TrySkipNextAsync();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
