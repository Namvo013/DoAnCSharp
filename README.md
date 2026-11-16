# LiMS - Library Management System

Hệ thống quản lý thư viện gồm Web cho độc giả, REST API dùng chung và ứng dụng quản trị Windows Forms. Cả Web và Admin đều gọi API; chỉ API kết nối Microsoft SQL Server.

## Cấu trúc dự án

| Thư mục / tệp | Nội dung |
|---|---|
| `LiMS.Api/` | ASP.NET Core Web API, EF Core, SQL Server, xác thực JWT và nghiệp vụ thư viện |
| `LiMS.Web/` | Website ASP.NET Core Razor Pages dành cho độc giả |
| `LiMS.Admin/` | Ứng dụng quản trị Windows Forms |
| `docs/requirements/` | PRD và đặc tả chức năng/giao diện |
| `docs/operations/KhoiDongWeb.txt` | Hướng dẫn cấu hình và khởi động SQL Server, API, Web, Admin |
| `docs/archive/` | Tài liệu tham khảo cho frontend HTML/JavaScript đời trước |
| `legacy-frontend/` | Giao diện HTML/CSS/JavaScript cũ; không phải frontend đang chạy |
| `start-admin.bat` | Khởi chạy ứng dụng Admin |
| `start-frontend.bat` | Khởi chạy website Razor Pages |

## Công nghệ

- C# / .NET 10
- ASP.NET Core Web API và Razor Pages
- Windows Forms
- Entity Framework Core
- Microsoft SQL Server
- JWT Bearer authentication

## Khởi chạy

1. Cấu hình và khởi động SQL Server theo [hướng dẫn khởi động](docs/operations/KhoiDongWeb.txt).
2. Tại thư mục dự án, mở terminal thứ nhất:

   ```powershell
   dotnet run --project ".\LiMS.Api\LiMS.Api.csproj" --launch-profile http
   ```

3. Mở terminal thứ hai để chạy Web:

   ```powershell
   .\start-frontend.bat
   ```

   Website: <http://localhost:5500>

4. Để chạy ứng dụng quản trị, dùng terminal thứ ba:

   ```powershell
   .\start-admin.bat
   ```

   API: <http://localhost:5107>; health check: <http://localhost:5107/api/library/health>

Tài khoản Admin ban đầu được tạo khi cấu hình database lần đầu. Không đưa mật khẩu hoặc JWT signing key vào GitHub; hãy dùng thông tin cấp riêng cho máy phát triển và đổi mật khẩu sau lần đăng nhập đầu tiên.

## Ghi chú

- Trước khi đưa lên GitHub, kiểm tra lại các tệp cấu hình để chắc chắn không có mật khẩu, khóa ký JWT hoặc dữ liệu cá nhân.
- Thư mục `legacy-frontend/` chỉ lưu mã giao diện tĩnh đời trước; chức năng hiện tại nằm trong `LiMS.Web/`.
- `bin/`, `obj/`, tệp cấu hình cá nhân và thông tin bí mật được loại trừ qua `.gitignore`.
