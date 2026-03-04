# Music2.0 - Tổng quan Dự án & Sơ đồ Kiến trúc

## 1. Bao Quát Kiến Trúc
**Music2.0** là một ứng dụng nghe nhạc trực tuyến đa nền tảng (Hybrid), sử dụng cấu trúc Backend API Proxy với **ASP.NET Core** làm cầu nối và giao diện tĩnh **Vanilla JavaScript SPA (Single Page Application)** làm Frontend.

Nguồn nhạc ban đầu lấy từ luồng ZingMp3. Tuy nhiên, do giới hạn bản quyền và API bị khóa nhiều tầng, hiện tại hệ thống đã được **Chuyển đổi hoàn toàn 100% để lấy trộm API chưa ghi danh (Undocumented) của SoundCloud (phiên bản v2)**. Đi kèm với nó là sự kết hợp luồng fallback phụ lấy nhạc stream trực tiếp từ **YouTube** (bỏ qua API key bằng tính năng `YoutubeExplode`) để phục vụ tính năng tìm kiếm rộng và chế độ hát Karaoke.

Triết lý cốt lõi của Kiến trúc này là giấu kín các API Key bằng cách ép mọi luồng kết nối Internet từ Frontend đi ngang qua `ApiController.cs` của Backend (để chặn lỗi **CORS** và giấu IP) trước khi đóng gói lại thành chuỗi JSON tiêu chuẩn đẩy xuống cho màn hình render.

## 2. Các Công Nghệ Trọng Tâm (Tech Stack)
*   **Backend:**
    *   Ngôn ngữ C# / ASP.NET Core 8+ (Áp dụng Dependency Injection, Controllers, Async/Task).
    *   Thư viện `YoutubeExplode` NuGet (Lấy luồng mp3 background không cần khóa API Google).
*   **Frontend:**
    *   HTML5 / Canvas / Thẻ `<audio>`
    *   Vanilla CSS nguyên bản (File hệ thống `site.css` - Quy tắc Custom Grid/Flex, Animations mạnh, Tự động thụt thò Dark theme, Không xài Tailwind hay Boostrap).
    *   Vanilla JavaScript (File `app.js`, `player.js` - Code lõi tay không xài React/Vue Frame nào cả).
*   **API Bên Thứ Ba Đang Cắm:**
    *   `api-v2.soundcloud.com/search/tracks` (Xử lý tìm kiếm nhạc generic, biểu đồ chart top 100, nhóm nhạc phát hành mới).
    *   `pipedapi.kavin.rocks/search` (Instances giả lập thay thế Youtube khi hệ chính quá tải).

## 3. Sơ Đồ Cấu Trúc Khung Thư Mục (Directory Structure)

```plaintext
C:\Users\acer\Downloads\Music2.0\
├── Controllers/
│   └── ApiController.cs       (Trung tâm chia làn Server Request Router)
├── Services/
│   ├── SoundCloudService.cs   (Gọi API gốc và map ép khuôn JSON mới)
│   └── ZingMp3Service.cs      (Hàng tồn kho / Cũ lỗi thời, CẤM DÙNG)
├── Program.cs                 (Trụ chính App & Khởi tạo Dependency Injection)
├── Properties/
│   └── launchSettings.json    (Profiles mạng)
├── bin/ & obj/                (Ra file Complied - CẤM ĐỤNG)
└── wwwroot/                   (Thư mục chứa Asset Tĩnh của Frontend-SPA)
    ├── index.html / _Layout   (Điểm nổ - Chứa giao diện markup và thẻ #hashes)
    ├── css/
    │   └── site.css           (Kiểm soát màu sắc, Responsive, Hover, Dark mode)
    └── js/
        ├── app.js             (Hạt nhân Router, Xây giao diện DOM, Nhúng Component)
        └── player.js          (Quy trình chạy nhạc Play/Pause, Events, Queue Logic)
```

## 4. Dòng Chảy Xử Lý Dữ Liệu (Data Flow)
1. **Tương Tác Người Dùng:** Click vào một phím chức năng trên UI (VD: `#/top100`).
2. **SPA Router (`app.js`):** `HashRouter` phân tích hành vi click đổi url và nổ cò khởi động lệnh cập nhật giao diện (`loadTop100()`).
3. **Frontend Gọi Data Lên:** `app.js` lập tức kích hoạt API cục bộ `fetchApi('/api/top100')` hiển thị con quay Spinner.
4. **Backend Mở Cổng (`ApiController.cs`):** Request bơi đến `[HttpGet("top100")]` nằm nội bộ trong Controller, nó lập tức hất lệnh cho class tiêm tĩnh `SoundCloudService`.
5. **Bay Ra Internet (`SoundCloudService.cs`):** Backend bí mật ném chuỗi GET ra Internet vào server SoundCloud qua địa chỉ `search/tracks` có gửi kèm Key Giả (Client ID) với thông số tìm kiếm.
6. **Ép Khuân Nhựa (JSON Mapper):** Sau khi SoundCloud trả file JSON lộn xộn, Hàm `MapTrack()` lập tức phẫu thuật, đổi tên và sắp xếp lại khối đó biến thành cấu trúc ZinMp3 Cũ. Cấu hình này giúp Frontend vẫn đọc được (VD: thẻ ảnh đổi thành `thumbnailM`, thẻ ID đổi là `encodeId`).
7. **Bơm Ngược Trả Cho UI:** Backend ném Code HTTP 200 nổ. Cục JSON chuẩn cập bến `app.js`, con quay Spinner biến mất. Trình kích lệnh đồ họa `_renderSongItem()` chạy vòng lặp in nhạc vào thẻ HTML trên màn hình!

## 5. Nguyên Tắc Thiết Kế Mẫu (Design Patterns Applicable)

### A. Tiêm Phụ Thuộc (Dependency Injection & Singleton) ở Backend
Cấu hình cứng tại `Program.cs`. Lớp `SoundCloudService` chạy dạng Singleton (Chạy một lần suốt đời). Nó ôm luôn Client của HttpClient tái sử dụng nhằm rào chống hiện tượng cạn kiệt Socket (Socket Exhaustion).

### B. Mô Phỏng Component (View Component / React-like UI)
Script `app.js` cấu hình theo việc sinh ra thành các đoạn chuỗi HTML String Component thay vì viết cứng. (Ví dụ hàm `_renderSongItem()` là cục list nhạc con, `_renderCard()` là thẻ bài phát sáng). Đặc thù này rất giống làm React/Vue nhưng thuần Vanilla JS. 

### C. Mẫu Proxy Xuyên Mảng (Surrogate Proxy Pattern)
Để đề phòng trình duyệt khóa URL cross-origin và lộ IP key xịn, toàn bộ việc fetch API đều ném lên lớp Middleware Proxy C# giải quyết. Dùng nó để bẻ cong lại dữ liệu JSON trước khi ném về lại JS.

---
**💡 Chỉ Dẫn Cuối Cùng Dành Riêng Cho AI Vận Hành Mới:**
- Cần sửa lỗi Click / Màu Sắc / Chữ Trưng Bày ->  Mở `wwwroot` (vào `.js` hoặc `.css`).
- Gãy luồng load nhạc báo 404, 500, lỗi hình ảnh bài hát không hiện lên -> Mở thư mục `Services/SoundCloudService.cs` fix file map JSON. 
- Yêu cầu cấu trúc: AI buộc phải in cấu trúc JSON giả từ thẻ Zing (VD `hasLyric`, `thumbnailM`) do JS Frontend bị ràng buộc sinh tử với các key này, KHÔNG được xuất dữ liệu API tự tạo thẳng vào Frontend.
