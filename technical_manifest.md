# Music2.0 - Khai báo Kỹ thuật (Technical Manifest)

## 1. Dịch vụ & Controller Cốt lõi (Backend)

### `ApiController` (C# ASP.NET Core)
*   **Đường dẫn:** `Controllers/ApiController.cs`
*   **Vai trò:** Cổng giao tiếp chính (API Gateway) kết nối giữa Frontend và các dịch vụ lấy dữ liệu. Xử lý các yêu cầu HTTP từ ứng dụng SPA (Single Page Application) và trả về dữ liệu chuẩn JSON.
*   **Các Hàm/Phương thức Chính:**
    *   `GetHome()`, `GetChart()`, `GetNewRelease()`, `GetTop100()`: Định tuyến các request cơ bản này đến `SoundCloudService`.
    *   `GetSongInfo(id)`, `GetSong(id)`: Lấy thông tin chi tiết của mp3 và đường dẫn (URL) audio stream trực tiếp.
    *   `GetArtist(name)`, `GetPlaylist(id)`: Tải dữ liệu trang chi tiết ca sĩ và playlist.
    *   `Search(q)`: Tìm kiếm toàn cục cho bài hát, ca sĩ, và playlist.
    *   `GetYoutubeFallback(id)`: Sử dụng thư viện `YoutubeExplode` để lấy luồng âm thanh trực tiếp từ YouTube (dùng để sửa lỗi khi SoundCloud từ chối phát nhạc hoặc dùng cho tính năng Karaoke).
    *   `SearchYoutube(q)`: Tìm kiếm video qua YouTube (vượt qua giới hạn hạn ngạch của Google API).

### `SoundCloudService` (C#)
*   **Đường dẫn:** `Services/SoundCloudService.cs`
*   **Vai trò:** Chịu trách nhiệm tương tác trực tiếp, gửi request lên API của SoundCloud. (Thay thế hoàn toàn cho `ZingMp3Service` cũ).
*   **Các Hàm/Phương thức Chính:**
    *   `GetAsync(endpoint, query)`: Cuộc gọi HTTP cơ sở dùng để lấy dữ liệu từ API SoundCloud.
    *   `GetCharts()`, `GetNewRelease()`, `GetTop100()`: Sử dụng endpoint `/search/tracks` kết hợp với các truy vấn chuyên biệt (`trending hits`, `new release pop`, `top 100`) để giả lập phân mục bảng xếp hạng.
    *   `MapTrack(JsonElement)`: Hàm siêu mấu chốt! Chuyển đổi và map (ánh xạ) dữ liệu trả về từ SoundCloud sang cấu trúc dữ liệu JSON kiểu cũ của ZingMp3, nhằm giúp Frontend nhận diện được dữ liệu hình ảnh, tên ca sĩ... mà không bị vỡ giao diện.

## 2. Các Module Cốt lõi (Frontend)

### Đối tượng `App` (Vanilla JS)
*   **Đường dẫn:** `wwwroot/js/app.js`
*   **Vai trò:** Xử lý điều hướng SPA (dựa trên URL Hash `#xyz`), kết xuất (render) mã HTML ra màn hình, kết nối API và xử lý sự kiện tương tác của người dùng.
*   **Các Hàm Chính:**
    *   `init()`: Khởi tạo các thành phần sự kiện, menu, và thanh nav.
    *   `navigate(page, params)` & `_handleHash()`: Logic điều hướng (Router) tải trang.
    *   `loadHome()`, `loadChart()`, `loadSearch(q)`, `loadTop100()`: Hàm fetch dữ liệu theo từng trang và gắn vào DOM.
    *   `_renderSongItem()`, `_renderCard()`, `_renderChartItem()`: Các component tái sử dụng (nhả ra HTML string) để xếp danh sách UI.
    *   `fetchApi(url)`: Cấu hình gộp gói truy vấn `fetch()` mặc định.

### Đối tượng `Player` (Vanilla JS)
*   **Đường dẫn:** `wwwroot/js/player.js`
*   **Vai trò:** Quản lý toàn bộ trình phát nhạc thanh dưới cùng, queue (hàng đợi), Volume, thanh thời gian, và các cơ chế chạy nền dự phòng.
*   **Các Hàm Chính:**
    *   `init()`: Gắn element `<audio>` và bắt sự kiện nút (play, next, prev, loop, shuffle).
    *   `playSong(song, playlist, index)`: Điểm vào chính để kích hoạt âm thanh của một bài nhạc mới.
    *   `_playHls(url)`, `_playNative(url)`: Giải mã và phát đuôi định dạng nhạc.
    *   `_tryYoutubeFallback(id)`: Hàm cứu hộ tự động chạy khi luồng nhạc chính bị lỗi (CORS, 403, 404). Backend sẽ gọi ngầm `/api/ytfallback` để lấy nhạc Youtube phát thế chỗ.

## 3. Danh sách API (Endpoints) Cần Lưu Ý

| Đường dẫn (URL) | Method | Mục đích | Dịch vụ Nội bộ |
| :--- | :--- | :--- | :--- |
| `/api/home` | GET | Tải danh sách giao diện trang chủ | `SoundCloudService.GetHome` |
| `/api/chart` | GET | Gọi bảng xếp hạng #top trending | `SoundCloudService.GetCharts` |
| `/api/newrelease`| GET | Nhạc mp3 phát hành mới | `SoundCloudService.GetNewRelease` |
| `/api/top100` | GET | Top 100 bài kinh điển nhất | `SoundCloudService.GetTop100` |
| `/api/song/info` | GET | Lấy thông tin metadata ảnh/ca sĩ | `SoundCloudService.GetSongInfo` |
| `/api/song` | GET | Link stream file nhạc gốc định dạng MP3 | `SoundCloudService.GetSong` |
| `/api/search` | GET | Search thông minh cho ô tìm kiếm | `SoundCloudService.Search` |
| `/api/ytfallback`| GET | Phương án phát nhạc khẩn cấp Youtube | Thư viện `YoutubeExplode` |
| `/api/ytsearch` | GET | Hệ tìm kiếm Youtube làm Karaoke | Thư viện `YoutubeExplode` |

## 4. Biến Môi Trường & Constant Cần Nhớ

*   **SoundCloud Client ID:** Nằm hardcode tại `SoundCloudService.cs` (VD: `u2ydppvwXCUxV6VITwH4OXk8JBySpoNr`). Khóa này bắt buộc phải có để trang chủ fetch được dữ liệu của SoundCloud.
*   **YouTube API Key (Cũ):** Biến `ytapi_key` trong file `app.js` (Đã chặn và ngưng xài vì giới hạn quota, chuyển sang dùng `YoutubeExplode` ở backend để gánh ngầm).
*   **URL Cơ sở gốc (SoundCloud):** `https://api-v2.soundcloud.com`

## 5. Từ Khóa Gợi Nhớ Cho AI Sau Này (Keywords)

*   **Từ khóa cần nạp:** `SoundCloud Migration`, `Vanilla JS SPA`, `YoutubeExplode`, `CORS Workarounds`, `Music Player UI Validation`, `Responsive Grid Layout`.
*   **Lưu ý cực kỳ quan trọng khi Dev:** Nếu UI tự dưng bị trắng trang và không báo lỗi, điều đầu tiên phải vào file `SoundCloudService.cs` kiểm tra hàm `MapTrack()`. Nguyên ngân là vì Frontend đang chờ được trả về chuẩn JSON dạng ZinMp3 Cũ (VD: `encodeId`, `artistsNames`, `thumbnailM`, `hasLyric`). Nếu Backend trả về thiếu key JSON, UI sẽ gãy ngay lập tức.
