# Kiểm thử tích hợp SQL Server và đo hiệu năng truy vấn

## Không thay đổi chức năng ứng dụng

Phần này chỉ bổ sung test, công cụ đo và hướng dẫn vận hành. Không đổi route, DTO, service, cấu hình runtime, UI hoặc cách người dùng thực hiện nghiệp vụ. Test tích hợp chỉ đọc metadata của database được cấp riêng.

## Bật kiểm thử tích hợp

Test SQL Server được đánh dấu bằng `SqlServerIntegrationFactAttribute`. Mặc định các test này được skip để lệnh test thông thường không vô tình kết nối tới database của nhóm. Chỉ bật khi đã có database riêng có tên chứa `test`:

```powershell
$env:LIMS_TEST_SQLSERVER = "Server=localhost;Database=LiMSTest;Integrated Security=true;TrustServerCertificate=true"
dotnet ef database update --project LiMS.Api --context LiMSDbContext
dotnet test LiMS.Tests/LiMS.Tests.csproj --filter FullyQualifiedName~SqlServerSchemaIntegrationTests
Remove-Item Env:LIMS_TEST_SQLSERVER
```

Không dùng connection string production. Bộ test xác nhận migration cuối đã áp dụng, check constraint đang bật và trusted, thứ tự key/include columns của các index, PK/FK/unique index, filtered unique index loan, và sự tồn tại của reporting views/procedures.

Các bài test database hiện chỉ đọc metadata. Chúng không tự tạo hoặc xóa database, không insert fixture, và không áp dụng migration tự động. Database cần được tạo riêng và migrate trước. Nếu biến môi trường vắng mặt hoặc tên DB không có `test`, xUnit hiển thị trạng thái Skipped thay vì giả báo Pass.

## Diễn giải kết quả tích hợp

Một lượt test thành công chỉ chứng minh database đang trỏ tới có schema objects mong đợi. Nó không chứng minh các view/procedure chạy tối ưu trên dữ liệu production và cũng không kiểm tra transaction song song.

Nếu test lỗi:

1. Xác minh connection string có tên database test.
2. Chạy `dotnet ef migrations list --project LiMS.Api --context LiMSDbContext`.
3. Áp dụng migration trên database test rồi kiểm tra lại migration history.
4. Chạy script `database/reader_views_and_procedures.sql` sau khi migration hoàn tất.
5. Chạy lại test metadata.

Không sửa schema bằng tay để làm test xanh trước khi xác định migration nào chưa áp dụng. Nếu đã có hotfix schema thủ công, cần tạo migration tương ứng và đối chiếu `__LiMSMigrationsHistory` trước.

## Đo hiệu năng có thể lặp lại

`database/reader_performance_diagnostics.sql` là script chỉ đọc. Nó chạy truy vấn đại diện cho catalog, lịch sử độc giả, khoản quá hạn, hàng đợi giữ sách, inbox, outbox và tồn kho; bật `STATISTICS IO`/`STATISTICS TIME`; cuối script liệt kê index usage và độ mới thống kê.

Để so sánh trước/sau:

1. Dùng bản restore cùng kích thước dữ liệu và cùng phiên bản SQL Server.
2. Chọn `FIXTURE-R-001` và đầu sách có dữ liệu thực tế đủ lớn; fixture nhỏ chỉ dùng để xác nhận script chạy.
3. Bật Actual Execution Plan trong SSMS.
4. Chạy 3–5 lần sau một lượt warm-up; ghi riêng cold-cache nếu chính sách môi trường cho phép. Không xóa cache trên môi trường chia sẻ.
5. Ghi logical reads, CPU, elapsed time, estimated/actual rows và operator scan/seek.
6. Lặp cùng tham số sau index hoặc cập nhật statistics; so sánh median và p95 ở ngoài transaction tải cao.
7. Kiểm tra `user_updates` cùng `user_seeks` để cân nhắc chi phí ghi của index.

Không đặt ngưỡng mili giây cố định trong unit test. Độ trễ phụ thuộc máy, cache, dữ liệu, tải và network; ngưỡng cứng dễ gây test chập chờn và che mất vấn đề plan. Lưu kết quả đo cùng build, phiên bản SQL Server, row count và ngày chạy khi cần báo cáo định lượng.

## Ma trận validation cấu hình

`ConfigValidationBoundaryTests` bổ sung kiểm tra min/max và dữ liệu sai cho các khóa hiện có trong `AdminService`:

| Cấu hình | Miền hợp lệ | Các trường hợp biên được test |
|---|---:|---|
| `LoanDurationDays` | 1–365 ngày | Hai biên; dưới min, trên max, số thập phân |
| `MaxRenewals` | 0–10 lần | Hai biên; số âm, trên max, rỗng/khoảng trắng |
| `ReservationPickupDays` | 1–30 ngày | Hai biên; dưới min, trên max |
| `MaxActiveLoans` | 1–100 phiếu | Hai biên; dưới min, trên max |
| `FinePerDay` | 0–1.000.000 VND | Hai biên; âm, trên max, scientific notation, text |

Các test xác nhận giá trị không hợp lệ không tạo config hoặc audit log. Không sửa thuật toán hay giới hạn hiện hành; nếu nghiệp vụ muốn đổi miền, cần sửa yêu cầu trước rồi cập nhật implementation và test cùng nhau.

## Kết quả kiểm thử của phiên

- Unit/service tests: chạy được cục bộ.
- SQL Server integration tests: hiện skip nếu không cung cấp `LIMS_TEST_SQLSERVER`; phiên này không có biến đó nên chưa có kết quả từ SQL Server thật.
- Performance diagnostics: script chưa được thực thi trên SQL Server trong phiên này; chưa có số liệu benchmark và không khẳng định mức tăng tốc.
- Config boundary tests: chạy trên EF InMemory để xác nhận validation service; không thay thế kiểm thử lưu trữ SQL Server.
