using Microsoft.Extensions.Caching.Memory;
using Music2._0.Models;
using YoutubeExplode;
using YoutubeExplode.Videos;

namespace Music2._0.Services;

public sealed class YouTubeMusicService
{
    private static readonly string[] VietnameseQueries =
    {
        "nhạc trẻ Việt Nam official audio",
        "VPOP official audio",
        "Sơn Tùng M-TP official",
        "nhạc Việt mới nhất official audio",
        "VPOP chill official audio",
        "nhạc Việt thịnh hành official"
    };

    private readonly YoutubeClient _youtube;
    private readonly IMemoryCache _cache;
    private readonly ILogger<YouTubeMusicService> _logger;

    public YouTubeMusicService(
        YoutubeClient youtube,
        IMemoryCache cache,
        ILogger<YouTubeMusicService> logger)
    {
        _youtube = youtube;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MusicTrack>> GetVietnameseTracksAsync(
        CancellationToken cancellationToken = default)
    {
        const string cacheKey = "youtube:vietnamese-home:v2";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<MusicTrack>? cached) &&
            cached is not null)
        {
            return cached;
        }

        var tracks = new List<MusicTrack>();
        foreach (var query in VietnameseQueries)
        {
            tracks.AddRange(await SearchAsync(query, 10, cancellationToken));
        }

        var result = tracks
            .GroupBy(track => track.EncodeId, StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(48)
            .ToArray();

        _cache.Set(
            cacheKey,
            result,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            });

        return result;
    }

    public async Task<IReadOnlyList<MusicTrack>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || limit <= 0)
        {
            return Array.Empty<MusicTrack>();
        }

        var cacheKey =
            $"youtube:search:{limit}:{query.Trim().ToLowerInvariant()}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<MusicTrack>? cached) &&
            cached is not null)
        {
            return cached;
        }

        try
        {
            var tracks = new List<MusicTrack>(limit);
            await foreach (var video in _youtube.Search
                .GetVideosAsync(query.Trim(), cancellationToken))
            {
                tracks.Add(MapTrack(video));
                if (tracks.Count >= limit)
                {
                    break;
                }
            }

            var result = tracks.ToArray();
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(20));
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "YouTube search failed for query {Query}.",
                query);
            return Array.Empty<MusicTrack>();
        }
    }

    public async Task<MusicTrack?> GetTrackAsync(
        string sourceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return null;
        }

        try
        {
            var video = await _youtube.Videos.GetAsync(sourceId, cancellationToken);
            return MapTrack(video);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "YouTube metadata lookup failed for video {VideoId}.",
                sourceId);
            return null;
        }
    }

    private static MusicTrack MapTrack(IVideo video)
    {
        var thumbnail = video.Thumbnails.LastOrDefault()?.Url;
        return new MusicTrack
        {
            EncodeId = ProviderTrackId.Create(
                ProviderTrackId.YouTube,
                video.Id.Value),
            Title = video.Title,
            ArtistsNames = video.Author.ChannelTitle,
            Thumbnail = thumbnail,
            ThumbnailM = thumbnail,
            Duration = (int)Math.Round(video.Duration?.TotalSeconds ?? 0),
            Artists = new[]
            {
                new MusicArtist { Name = video.Author.ChannelTitle }
            },
            Source = ProviderTrackId.YouTube
        };
    }
}
