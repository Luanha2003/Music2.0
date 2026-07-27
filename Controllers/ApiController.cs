using Microsoft.AspNetCore.Mvc;
using Music2._0.Services;

namespace Music2._0.Controllers;

[Route("api")]
[ApiController]
public class ApiController : ControllerBase
{
    private readonly MusicService _music;
    private readonly OpenSubsonicService _openSubsonic;

    public ApiController(
        MusicService music,
        OpenSubsonicService openSubsonic)
    {
        _music = music;
        _openSubsonic = openSubsonic;
    }

    [HttpGet("home")]
    public async Task<IActionResult> GetHome(CancellationToken cancellationToken)
    {
        var data = await _music.GetHomeAsync(cancellationToken);
        return data is not null
            ? Ok(data)
            : StatusCode(502, new { error = "Không thể lấy dữ liệu trang chủ" });
    }

    [HttpGet("song/{id}")]
    public async Task<IActionResult> GetSong(
        string id,
        CancellationToken cancellationToken)
    {
        var data = await _music.GetSongAsync(id, cancellationToken);
        return data is not null
            ? Ok(data)
            : StatusCode(502, new { error = "Không thể lấy link nhạc" });
    }

    [HttpGet("songinfo/{id}")]
    public async Task<IActionResult> GetSongInfo(
        string id,
        CancellationToken cancellationToken)
    {
        var data = await _music.GetSongInfoAsync(id, cancellationToken);
        return data is not null
            ? Ok(data)
            : StatusCode(502, new { error = "Không thể lấy thông tin bài hát" });
    }

    [HttpGet("lyrics/{id}")]
    public async Task<IActionResult> GetLyrics(
        string id,
        CancellationToken cancellationToken)
    {
        return Ok(await _music.GetLyricsAsync(id, cancellationToken));
    }

    [HttpGet("chart")]
    public async Task<IActionResult> GetChart(CancellationToken cancellationToken)
    {
        var data = await _music.GetChartsAsync(cancellationToken);
        return data is not null
            ? Ok(data)
            : StatusCode(502, new { error = "Không thể lấy bảng xếp hạng" });
    }

    [HttpGet("newrelease")]
    public async Task<IActionResult> GetNewRelease(CancellationToken cancellationToken)
    {
        var data = await _music.GetNewReleasesAsync(cancellationToken);
        return data is not null
            ? Ok(data)
            : StatusCode(502, new { error = "Không thể lấy nhạc mới" });
    }

    [HttpGet("top100")]
    public async Task<IActionResult> GetTop100(CancellationToken cancellationToken)
    {
        var data = await _music.GetTop100Async(cancellationToken);
        return data is not null
            ? Ok(data)
            : StatusCode(502, new { error = "Không thể lấy Top 100" });
    }

    [HttpGet("artist/{alias}")]
    public async Task<IActionResult> GetArtist(
        string alias,
        CancellationToken cancellationToken)
    {
        var data = await _music.SearchAsync(alias, cancellationToken);
        return data is not null
            ? Ok(data)
            : StatusCode(502, new { error = "Không tìm thấy ca sĩ" });
    }

    [HttpGet("playlist/{id}")]
    public async Task<IActionResult> GetPlaylist(
        string id,
        CancellationToken cancellationToken)
    {
        var data = await _music.GetSongInfoAsync(id, cancellationToken);
        return data is not null
            ? Ok(data)
            : StatusCode(502, new { error = "Không thể lấy playlist" });
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { error = "Vui lòng nhập từ khóa tìm kiếm" });
        }

        var data = await _music.SearchAsync(q, cancellationToken);
        return data is not null
            ? Ok(data)
            : StatusCode(502, new { error = "Không thể tìm kiếm" });
    }

    [HttpGet("providers")]
    public async Task<IActionResult> GetProviders(CancellationToken cancellationToken)
    {
        return Ok(await _music.GetProviderStatusAsync(cancellationToken));
    }

    [HttpGet("suggest")]
    public IActionResult Suggest([FromQuery] string q)
    {
        return Ok(new { data = Array.Empty<object>() });
    }

    [HttpGet("hubhome")]
    public Task<IActionResult> GetHubHome(CancellationToken cancellationToken)
    {
        return GetHome(cancellationToken);
    }

    [HttpGet("hubdetail/{id}")]
    public Task<IActionResult> GetHubDetail(
        string id,
        CancellationToken cancellationToken)
    {
        return GetHome(cancellationToken);
    }

    [HttpGet("stream/{id}")]
    public async Task StreamOpenSubsonic(
        string id,
        CancellationToken cancellationToken)
    {
        if (!ProviderTrackId.TryParse(id, out var provider, out var sourceId) ||
            provider != ProviderTrackId.OpenSubsonic)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        using var upstream = await _openSubsonic.GetStreamResponseAsync(
            sourceId,
            Request.Headers.Range.ToString(),
            cancellationToken);

        await CopyUpstreamResponseAsync(upstream, cancellationToken);
    }

    [HttpGet("cover/{id}")]
    public async Task CoverOpenSubsonic(
        string id,
        CancellationToken cancellationToken)
    {
        if (!ProviderTrackId.TryParse(id, out var provider, out var coverArtId) ||
            provider != ProviderTrackId.OpenSubsonicCover)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        using var upstream = await _openSubsonic.GetCoverResponseAsync(
            coverArtId,
            cancellationToken);

        if (upstream is not null && upstream.IsSuccessStatusCode)
        {
            Response.Headers.CacheControl = "public,max-age=86400";
        }

        await CopyUpstreamResponseAsync(upstream, cancellationToken);
    }

    [HttpGet("ytfallback")]
    public async Task<IActionResult> GetYoutubeFallback([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest();
        }

        try
        {
            var youtube = new YoutubeExplode.YoutubeClient();
            YoutubeExplode.Search.VideoSearchResult? video = null;
            await foreach (var result in youtube.Search.GetVideosAsync(q))
            {
                video = result;
                break;
            }

            if (video is null)
            {
                return NotFound();
            }

            var manifest = await youtube.Videos.Streams.GetManifestAsync(video.Id);
            var streamInfo = manifest.GetAudioOnlyStreams()
                .OrderByDescending(stream => stream.Bitrate)
                .FirstOrDefault();
            return streamInfo is null
                ? NotFound()
                : Ok(new { url = streamInfo.Url });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("ytsearch")]
    public async Task<IActionResult> SearchYoutube([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest();
        }

        try
        {
            var youtube = new YoutubeExplode.YoutubeClient();
            var results = new List<object>();
            await foreach (var video in youtube.Search.GetVideosAsync(q))
            {
                results.Add(new
                {
                    id = new { videoId = video.Id.Value },
                    snippet = new
                    {
                        title = video.Title,
                        thumbnails = new
                        {
                            medium = new { url = video.Thumbnails.FirstOrDefault()?.Url }
                        }
                    }
                });

                if (results.Count >= 12)
                {
                    break;
                }
            }

            return Ok(new { items = results });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private async Task CopyUpstreamResponseAsync(
        HttpResponseMessage? upstream,
        CancellationToken cancellationToken)
    {
        if (upstream is null)
        {
            Response.StatusCode = StatusCodes.Status502BadGateway;
            return;
        }

        Response.StatusCode = (int)upstream.StatusCode;
        if (upstream.Content.Headers.ContentType is not null)
        {
            Response.ContentType = upstream.Content.Headers.ContentType.ToString();
        }

        if (upstream.Content.Headers.ContentLength.HasValue)
        {
            Response.ContentLength = upstream.Content.Headers.ContentLength.Value;
        }

        if (upstream.Content.Headers.ContentRange is not null)
        {
            Response.Headers.ContentRange =
                upstream.Content.Headers.ContentRange.ToString();
        }

        if (upstream.Headers.AcceptRanges.Count > 0)
        {
            Response.Headers.AcceptRanges =
                string.Join(",", upstream.Headers.AcceptRanges);
        }

        await upstream.Content.CopyToAsync(Response.Body, cancellationToken);
    }
}
