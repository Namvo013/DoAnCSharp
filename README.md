Link gg docs quá trình làm bài của nhóm:
https://docs.google.com/document/d/1LlYWdx2fwALSF9xYvVSEyiGO4XDABOZBHCllu9ZDjXU/edit?usp=sharing
# LiMS - Hệ thống quản lý thư viện

## 1. Giới thiệu

LiMS là hệ thống quản lý thư viện được xây dựng bằng C#/.NET. Hệ thống gồm website dành cho độc giả, dịch vụ REST API dùng chung và ứng dụng quản trị Windows Forms dành cho thủ thư và quản trị viên.

Cả website và ứng dụng quản trị đều giao tiếp với REST API. Chỉ thành phần API trực tiếp kết nối với cơ sở dữ liệu Microsoft SQL Server.

## 2. Cấu trúc dự án

| Thư mục / tệp | Chức năng |
|---|---|
| `LiMS.Api/` | ASP.NET Core Web API, Entity Framework Core, SQL Server, xác thực JWT và xử lý nghiệp vụ thư viện |
| `LiMS.Web/` | Website ASP.NET Core Razor Pages dành cho độc giả |
| `LiMS.Admin/` | Ứng dụng quản trị Windows Forms |
| `docs/requirements/` | Tài liệu yêu cầu và đặc tả chức năng, giao diện |
| `docs/operations/KhoiDongWeb.txt` | Hướng dẫn cấu hình và khởi động SQL Server, API, Web, Admin |
| `docs/archive/` | Tài liệu tham khảo cho frontend HTML/JavaScript đời trước |
| `legacy-frontend/` | Giao diện HTML/CSS/JavaScript cũ, không phải frontend đang hoạt động |
| `start-admin.bat` | Khởi chạy ứng dụng quản trị |
| `start-frontend.bat` | Khởi chạy website Razor Pages |
| `Start-LiMS.ps1` | Kiểm tra Backend và khởi chạy ứng dụng được chọn |
| `tai-lieu/` | Tài liệu phân tích lớp thực thể và mối quan hệ dữ liệu |

## 3. Công nghệ sử dụng

- Ngôn ngữ lập trình: C#
- Nền tảng: .NET 10
- Dịch vụ phía máy chủ: ASP.NET Core Web API
- Giao diện độc giả: ASP.NET Core Razor Pages
- Ứng dụng quản trị: Windows Forms
- Truy cập dữ liệu: Entity Framework Core
- Hệ quản trị cơ sở dữ liệu: Microsoft SQL Server
- Cơ chế xác thực: JWT Bearer Authentication

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

### 5.1. Chuẩn bị cơ sở dữ liệu

Cấu hình và khởi động Microsoft SQL Server theo tài liệu:

`docs/operations/KhoiDongWeb.txt`

Thiết lập chuỗi kết nối phù hợp với môi trường phát triển trước khi chạy ứng dụng.

### 5.2. Khởi chạy website

Mở PowerShell tại thư mục gốc dự án và chạy:

```powershell
.\start-frontend.bat
```

Website: http://localhost:5500

### 5.3. Khởi chạy ứng dụng quản trị

Chạy lệnh:

```powershell
.\start-admin.bat
```

API: http://localhost:5107

Health check: http://localhost:5107/api/library/health

### 5.4. Tài khoản quản trị

Tài khoản Admin ban đầu được tạo khi cấu hình cơ sở dữ liệu lần đầu. Không đưa mật khẩu hoặc khóa ký JWT lên GitHub.

## 6. Tài liệu trong kho mã nguồn

- [Danh sách lớp thực thể và các mối quan hệ](tai-lieu/danh-sach-lop-va-moi-quan-he.md)
- `docs/requirements/`: tài liệu yêu cầu và đặc tả.
- `docs/operations/`: tài liệu hướng dẫn vận hành.
- `docs/archive/`: tài liệu tham khảo của các phiên bản giao diện trước.

## 7. Lưu ý

- Không đưa mật khẩu, khóa bí mật, chuỗi kết nối chứa thông tin đăng nhập hoặc dữ liệu cá nhân thật lên kho mã nguồn.
- Không đưa các thư mục kết quả biên dịch như `bin/`, `obj/` và tệp cấu hình cá nhân lên GitHub.
- `legacy-frontend/` chỉ lưu mã giao diện cũ; giao diện web hiện tại nằm trong `LiMS.Web/`.
