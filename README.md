# LiMS - Hệ thống quản lý thư viện

## 1. Giới thiệu

LiMS là hệ thống quản lý thư viện được xây dựng bằng C#/.NET. Dự án gồm trang web dành cho độc giả, dịch vụ xử lý nghiệp vụ dùng chung và ứng dụng máy tính dành cho thủ thư, quản trị viên.

Các thành phần giao tiếp với nhau thông qua dịch vụ Web API. Thành phần API chịu trách nhiệm xử lý nghiệp vụ và truy cập cơ sở dữ liệu Microsoft SQL Server.

## 2. Cấu trúc dự án

| Thư mục | Chức năng |
|---|---|
| `LiMS.Api/` | Dịch vụ Web API, xử lý nghiệp vụ, xác thực và truy cập dữ liệu |
| `LiMS.Web/` | Trang web dành cho độc giả, xây dựng bằng ASP.NET Core Razor Pages |
| `LiMS.Admin/` | Ứng dụng quản trị trên máy tính, sử dụng Windows Forms |
| `docs/` | Tài liệu yêu cầu, hướng dẫn vận hành và tài liệu tham khảo |
| `legacy-frontend/` | Giao diện HTML/CSS/JavaScript cũ được lưu để tham khảo |

## 3. Công nghệ sử dụng

- Ngôn ngữ lập trình: C#
- Nền tảng: .NET 10
- Dịch vụ phía máy chủ: ASP.NET Core Web API
- Giao diện độc giả: ASP.NET Core Razor Pages
- Ứng dụng quản trị: Windows Forms
- Truy cập dữ liệu: Entity Framework Core
- Hệ quản trị cơ sở dữ liệu: Microsoft SQL Server
- Cơ chế xác thực: JWT

## 4. Nhóm chức năng

### 4.1. Chức năng dành cho độc giả

- Đăng nhập và quản lý thông tin tài khoản.
- Tra cứu đầu sách và thông tin bản sao sách.
- Theo dõi phiếu mượn, hạn trả và lịch sử mượn.
- Đặt trước sách và theo dõi trạng thái đặt trước.
- Gửi đề xuất bổ sung sách.
- Theo dõi khoản phạt và thông báo liên quan.

### 4.2. Chức năng dành cho thủ thư và quản trị viên

- Quản lý đầu sách, thể loại và bản sao sách.
- Quản lý thông tin độc giả và thẻ thư viện.
- Lập phiếu mượn, ghi nhận trả sách và gia hạn.
- Quản lý yêu cầu đặt trước.
- Theo dõi và thu các khoản phạt.
- Xem thống kê, nhật ký thao tác và cấu hình hệ thống.
- Xem xét đề xuất bổ sung sách.

## 5. Hướng dẫn khởi chạy

Trước khi khởi chạy, cần cấu hình Microsoft SQL Server và chuỗi kết nối theo tài liệu vận hành có trong thư mục `docs/`.

Mở PowerShell tại thư mục gốc dự án:

Khởi chạy trang web:

```powershell
.\start-frontend.bat
```

Khởi chạy ứng dụng quản trị:

```powershell
.\start-admin.bat
```

Các địa chỉ dịch vụ và thông tin cấu hình cụ thể được trình bày trong tài liệu hướng dẫn khởi động của dự án.

## 6. Tài liệu trong kho mã nguồn

- [Danh sách lớp thực thể và các mối quan hệ](tai-lieu/danh-sach-lop-va-moi-quan-he.md)
- Thư mục `docs/`: tài liệu yêu cầu và hướng dẫn vận hành hiện có.

## 7. Lưu ý

- Không đưa mật khẩu, khóa bí mật, chuỗi kết nối có thông tin đăng nhập hoặc dữ liệu cá nhân thật lên kho mã nguồn.
- Không đưa các thư mục kết quả biên dịch và tệp tạm như `bin/`, `obj/` lên kho mã nguồn.
- `legacy-frontend/` là giao diện cũ dùng để tham khảo; giao diện web hiện tại nằm trong `LiMS.Web/`.
