namespace Music2._0.Models;

public sealed class MusicTrack
{
    public required string EncodeId { get; init; }
    public required string Title { get; init; }
    public required string ArtistsNames { get; init; }
    public string? Thumbnail { get; init; }
    public string? ThumbnailM { get; init; }
    public int Duration { get; init; }
    public required IReadOnlyList<MusicArtist> Artists { get; init; }
    public required string Source { get; init; }
    public string? Album { get; init; }
}

public sealed class MusicArtist
{
    public required string Name { get; init; }
}

public sealed class LyricLine
{
    public required string Text { get; init; }
    public long StartTime { get; init; }
}
