# Mr.T CAD Toolset

Add-in cho AutoCAD 2024 x64. Bản thử **2.0.2.7** kế thừa 2.0.2.2. [Tải bộ cài offline](releases/MrT_CAD_Toolset_2.0.2.7_Setup.exe).

## Bắt đầu

- `src/`: mã nguồn Core, Engine, UI và plugin AutoCAD.
- `packaging/`, `build.ps1`, `package.ps1`: công cụ build và đóng gói setup offline.
- `profiles/`: cấu hình mặc định rỗng; không nạp trước danh mục hệ ống vào DWG.
- `tests/`, `reference-originals/`: kiểm thử và mẫu nét tham chiếu. `tests/room_fixture.xlsx` là dữ liệu giả để kiểm thử, không phải danh sách phòng của người dùng.
- `evidence/`: các ảnh giao diện mẫu cần cho hướng dẫn trong bộ cài.
- `releases/`: các bộ cài đã bàn giao.
- [Bàn giao phát triển](HANDOFF.md): kết quả, hạn chế và yêu cầu tiếp theo.

Build trên Windows x64 có AutoCAD 2024 đầy đủ, .NET Framework và bộ công cụ Windows Installer theo `build.ps1`. Từ PowerShell, chạy `./build.ps1`, sau đó `./package.ps1 -SkipBuild`. Các kiểm thử native cần AutoCAD; không coi việc biên dịch là xác nhận chạy đúng trong DWG. Nếu máy không cài AutoCAD tại vị trí mặc định, xem tham số `-AutoCADPath`.

Dùng build.ps1 để biên dịch và chạy kiểm thử trước khi đóng gói. Solution cũng hỗ trợ Release|x64 với AutoCAD 2024 và .NET Framework 4.8.

Kho chỉ chứa mã, bộ cài và dữ liệu thử cần thiết. Bản vẽ DWG thực tế, Excel công việc, thông tin đăng nhập và log cá nhân phải quản lý riêng.

## Làm song song

`main` là điểm gốc chung. Trước khi bắt đầu, cập nhật `main` rồi tạo nhánh riêng, ví dụ `home/ten-phong` hoặc `office/kiem-tra-bang`. Làm và đẩy các nhánh riêng; xem diff, kiểm thử và hợp nhất khi hai công việc sẵn sàng. Không cùng sửa `main` trực tiếp từ hai máy. Cập nhật `HANDOFF.md` khi có quyết định mới hoặc kết quả kiểm thử quan trọng.
