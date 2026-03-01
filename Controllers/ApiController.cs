using Microsoft.AspNetCore.Mvc;
using Music2._0.Services;

namespace Music2._0.Controllers;

[Route("api")]
[ApiController]
public class ApiController : ControllerBase
{
    private readonly ZingMp3Service _zing;

    public ApiController(ZingMp3Service zing)
    {
        _zing = zing;
    }

    [HttpGet("home")]
    public async Task<IActionResult> GetHome()
    {
        var data = await _zing.GetHome();
        return data != null ? Ok(data) : StatusCode(502, new { error = "Không thể lấy dữ liệu trang chủ" });
    }

    [HttpGet("song/{id}")]
    public async Task<IActionResult> GetSong(string id)
    {
        var data = await _zing.GetSong(id);
        return data != null ? Ok(data) : StatusCode(502, new { error = "Không thể lấy link nhạc" });
    }

    [HttpGet("songinfo/{id}")]
    public async Task<IActionResult> GetSongInfo(string id)
    {
        var data = await _zing.GetSongInfo(id);
        return data != null ? Ok(data) : StatusCode(502, new { error = "Không thể lấy thông tin bài hát" });
    }

    [HttpGet("lyrics/{id}")]
    public async Task<IActionResult> GetLyrics(string id)
    {
        var data = await _zing.GetLyric(id);
        return data != null ? Ok(data) : StatusCode(502, new { error = "Không thể lấy lời bài hát" });
    }

    [HttpGet("chart")]
    public async Task<IActionResult> GetChart()
    {
        var data = await _zing.GetChartHome();
        return data != null ? Ok(data) : StatusCode(502, new { error = "Không thể lấy bảng xếp hạng" });
    }

    [HttpGet("newrelease")]
    public async Task<IActionResult> GetNewRelease()
    {
        var data = await _zing.GetNewReleaseChart();
        return data != null ? Ok(data) : StatusCode(502, new { error = "Không thể lấy nhạc mới" });
    }

    [HttpGet("top100")]
    public async Task<IActionResult> GetTop100()
    {
        var data = await _zing.GetTop100();
        return data != null ? Ok(data) : StatusCode(502, new { error = "Không thể lấy Top 100" });
    }

    [HttpGet("artist/{alias}")]
    public async Task<IActionResult> GetArtist(string alias)
    {
        var data = await _zing.GetArtist(alias);
        return data != null ? Ok(data) : StatusCode(502, new { error = "Không tìm thấy ca sĩ" });
    }

    [HttpGet("playlist/{id}")]
    public async Task<IActionResult> GetPlaylist(string id)
    {
        var data = await _zing.GetPlaylist(id);
        return data != null ? Ok(data) : StatusCode(502, new { error = "Không thể lấy playlist" });
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Vui lòng nhập từ khóa tìm kiếm" });

        var data = await _zing.SearchAll(q);
        return data != null ? Ok(data) : StatusCode(502, new { error = "Không thể tìm kiếm" });
    }

    [HttpGet("suggest")]
    public async Task<IActionResult> Suggest([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(new { data = Array.Empty<object>() });

        var data = await _zing.GetSuggestion(q);
        return data != null ? Ok(data) : StatusCode(502, new { error = "Lỗi gợi ý" });
    }

    [HttpGet("hubhome")]
    public async Task<IActionResult> GetHubHome()
    {
        var data = await _zing.GetHubHome();
        return data != null ? Ok(data) : StatusCode(502, new { error = "Lỗi hub" });
    }

    [HttpGet("hubdetail/{id}")]
    public async Task<IActionResult> GetHubDetail(string id)
    {
        var data = await _zing.GetHubDetail(id);
        return data != null ? Ok(data) : StatusCode(502, new { error = "Lỗi hub detail" });
    }

    [HttpGet("ytfallback")]
    public async Task<IActionResult> GetYoutubeFallback([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q)) return BadRequest();
        try
        {
            var youtube = new YoutubeExplode.YoutubeClient();
            YoutubeExplode.Search.VideoSearchResult video = null;
            await foreach(var v in youtube.Search.GetVideosAsync(q))
            {
                video = v;
                break;
            }
            if (video == null) return NotFound();
            
            var streamManifest = await youtube.Videos.Streams.GetManifestAsync(video.Id);
            var streamInfo = streamManifest.GetAudioOnlyStreams().OrderByDescending(s => s.Bitrate).FirstOrDefault();
            if (streamInfo == null) return NotFound();
            
            return Ok(new { url = streamInfo.Url });
        }
        catch(Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("ytsearch")]
    public async Task<IActionResult> SearchYoutube([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q)) return BadRequest();
        try
        {
            var youtube = new YoutubeExplode.YoutubeClient();
            var results = new System.Collections.Generic.List<object>();
            await foreach(var v in youtube.Search.GetVideosAsync(q))
            {
                results.Add(new {
                    id = new { videoId = v.Id.Value },
                    snippet = new {
                        title = v.Title,
                        thumbnails = new {
                            medium = new { url = System.Linq.Enumerable.FirstOrDefault(v.Thumbnails)?.Url }
                        }
                    }
                });
                if (results.Count >= 12) break;
            }
            return Ok(new { items = results });
        }
        catch(Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
