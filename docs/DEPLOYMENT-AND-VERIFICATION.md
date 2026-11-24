# Cấu hình triển khai và kiểm tra LiMS

## Cấu hình môi trường

`LiMS.Api` không còn chứa connection string trong cấu hình mặc định. Khi chạy Development, `appsettings.Development.json` trỏ đến SQL Server Express cục bộ. Các môi trường khác phải cấp cấu hình qua secret store hoặc biến môi trường, không commit thông tin xác thực.

Các biến môi trường cần thiết:

```powershell
$env:ConnectionStrings__LiMS = "Server=<sql-host>;Database=LiMSDB;User Id=<user>;Password=<secret>;Encrypt=True;TrustServerCertificate=False"
$env:Jwt__SigningKey = "<base64-encoded-random-key-of-at-least-32-bytes>"
$env:Jwt__Issuer = "LiMS.Api"
$env:Jwt__Audience = "LiMS.Clients"
$env:LiMSApi__BaseUrl = "https://<api-host>/"
```

`LiMS.Web` kiểm tra URL API lúc khởi động và từ chối HTTP ngoài Development. Giá trị localhost chỉ có trong `LiMS.Web/appsettings.Development.json`. `LiMS.Api` kiểm tra khóa JWT và connection string khi khởi động.

## Kiểm tra kết nối và migration

Sau khi cấu hình SQL Server và khởi chạy API, gọi `GET /api/library/health`. Đáp ứng mong đợi là `status: "ok"` và `database: true`. API áp dụng migration khi khởi động theo `LiMS.Api/Data/DatabaseInitializer.cs`; hãy sao lưu database và triển khai migration theo quy trình quản trị của môi trường trước khi phát hành.

Nguồn schema hiện tại là EF Core migrations trong `LiMS.Api/Data/Migrations`. `database/LiMSDB-schema-snapshot-20261115.sql` là snapshot lịch sử, không dùng để tạo database mới. Để triển khai thủ công, cấp `ConnectionStrings__LiMS` đúng môi trường rồi chạy `dotnet ef database update --project LiMS.Api --context LiMSDbContext`.

Kiểm tra tích hợp SQL Server chỉ chạy khi có biến `LIMS_TEST_SQLSERVER` trỏ tới database riêng có tên chứa `test`. Không dùng database thật hoặc database phát triển cho test vì test có thể tạo/thay đổi schema.

```powershell
$env:LIMS_TEST_SQLSERVER = "Server=<sql-host>;Database=LiMSTest;User Id=<user>;Password=<secret>;Encrypt=True;TrustServerCertificate=False"
dotnet test LiMS.Tests/LiMS.Tests.csproj --no-restore --verbosity minimal
```

## Kiểm tra giao diện responsive

Website có breakpoint ở 1100, 1000, 900, 760, 700 và 600px trong `LiMS.Web/wwwroot/css`. Trước phát hành, kiểm tra đăng nhập, trang chủ, danh mục, chi tiết sách, phiếu mượn, đặt giữ, thông báo, hồ sơ và biểu mẫu đề xuất ở các viewport rộng 360px, 390px, 768px, 1024px và desktop. Kiểm tra menu điện thoại, không tràn ngang, thao tác bàn phím và zoom 200%.

WinForms có trang `Thể loại sách` cho phép thêm, đổi tên và xóa thể loại chưa được sử dụng. API từ chối xóa thể loại còn đầu sách liên kết.

## Kiến trúc hiện tại và bước tách lớp

Solution đã tách theo ứng dụng (`LiMS.Admin`, `LiMS.Web`, `LiMS.Api`, `LiMS.Tests`). Bên trong `LiMS.Api`, controllers, services, data, models và contracts vẫn cùng một project. Đây là mức phân lớp hiện tại; cần giữ các tầng phụ thuộc một chiều khi mở rộng.

Nếu bắt đầu tách thành nhiều project, làm theo từng bước riêng: chuyển entity và enum thuần sang `LiMS.Domain`; chuyển interface/DTO và use case sang `LiMS.Application`; chuyển `DbContext`, migrations và service triển khai sang `LiMS.Infrastructure`; để `LiMS.Api` chỉ còn HTTP/authentication/composition root. Mỗi bước cần cập nhật project references và chạy toàn bộ unit/integration tests trước khi gộp. Không tách đồng loạt trong đợt cấu hình này vì `Services` hiện trực tiếp dùng `LiMSDbContext` và contract/entity liên kết chặt; thay đổi lớn cần được chia nhỏ để tránh phá các luồng quản trị đang hoạt động.
