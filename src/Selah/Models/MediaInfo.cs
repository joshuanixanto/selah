namespace Selah.Models;

public sealed record MediaInfo(
    string Title,
    string Artist,
    string Album,
    string AppName,
    bool IsPlaying,
    TimeSpan Position,
    TimeSpan Duration,
    byte[]? ArtworkBytes = null);
