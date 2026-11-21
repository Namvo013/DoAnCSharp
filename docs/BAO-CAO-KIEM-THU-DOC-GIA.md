# Báo cáo kiểm thử phân hệ Độc giả

## Phạm vi

Báo cáo này ghi lại phạm vi kiểm thử tự động cho các phần liên quan đến độc giả trong `LiMS.Api`, `LiMS.Web` và các hợp đồng được chia sẻ. Test chạy trên xUnit với EF Core InMemory; các kiểm thử không cần cơ sở dữ liệu SQL Server hoặc thông tin đăng nhập thật.

Các tệp chính được rà soát:

- `LiMS.Tests/ReaderServiceWorkflowTests.cs`
- `LiMS.Tests/LibraryQueryServiceTests.cs`
- `LiMS.Tests/NotificationServiceTests.cs`
- `LiMS.Tests/NotificationSchedulerTests.cs`
- `LiMS.Tests/AuthorizationAndClientTests.cs`
- `LiMS.Tests/ReaderDatabaseModelTests.cs`
- `LiMS.Tests/ApiRouteContractTests.cs`
- `LiMS.Tests/RequestValidationTests.cs`

## Ma trận tình huống

| Mã | Nhóm | Tình huống được kiểm tra | Kết quả mong đợi | Kết quả chạy |
|---|---|---|---|---|
| RD-001 | Hồ sơ | Cập nhật họ tên, email và số điện thoại có khoảng trắng | Dữ liệu được chuẩn hóa trước khi lưu | PASS |
| RD-002 | Hồ sơ | Số điện thoại chỉ chứa khoảng trắng | Lưu giá trị rỗng dưới dạng `null` | PASS |
| RD-003 | Hồ sơ | Email đã thuộc tài khoản khác | Từ chối bằng lỗi nghiệp vụ | PASS |
| RD-004 | Hồ sơ | Tài khoản không có hồ sơ độc giả | Báo không tìm thấy hồ sơ | PASS |
| RD-005 | Hồ sơ | Cập nhật thành công | Ghi nhật ký kiểm toán | PASS |
| RD-006 | Đề xuất | Độc giả hoạt động gửi nội dung có khoảng trắng | Lưu nội dung đã trim và trạng thái Pending | PASS |
| RD-007 | Đề xuất | Tài khoản độc giả bị vô hiệu hóa | Từ chối tạo đề xuất | PASS |
| RD-008 | Đề xuất | Thẻ độc giả không hoạt động | Từ chối tạo đề xuất | PASS |
| RD-009 | Đề xuất | Đọc đề xuất của độc giả khác | Không trả dữ liệu chéo tài khoản | PASS |
| RD-010 | Đặt giữ | Sách hết bản sao lưu thông | Tạo yêu cầu Pending gắn đúng độc giả/sách | PASS |
| RD-011 | Đặt giữ | Sách đang có bản sao Available | Từ chối đặt giữ | PASS |
| RD-012 | Đặt giữ | Đầu sách không có bản sao OnLoan | Từ chối đặt giữ | PASS |
| RD-013 | Đặt giữ | Độc giả đã có yêu cầu đang hiệu lực | Từ chối yêu cầu trùng | PASS |
| RD-014 | Đặt giữ | Thẻ độc giả bị khóa | Từ chối đặt giữ | PASS |
| RD-015 | Đặt giữ | Mã đầu sách không tồn tại | Trả lỗi không tìm thấy | PASS |
| RD-016 | Đặt giữ | Truy vấn phiếu của chủ sở hữu | Trả đúng thông tin đầu sách | PASS |
| RD-017 | Đặt giữ | Tài khoản khác đọc phiếu | Không trả dữ liệu phiếu | PASS |
| RD-018 | Hủy phiếu | Chủ sở hữu hủy phiếu Pending | Chuyển trạng thái Cancelled và ghi audit | PASS |
| RD-019 | Hủy phiếu | Tài khoản khác hủy phiếu | Từ chối và giữ nguyên trạng thái | PASS |
| RD-020 | Hủy phiếu | Hủy phiếu đã hoàn tất | Từ chối chuyển trạng thái | PASS |
| RD-021 | Hủy phiếu | Hủy lượt ReadyForPickup | Yêu cầu service thăng hạng lượt kế tiếp | PASS |
| RD-022 | Gia hạn | Phiếu còn hiệu lực, còn lượt gia hạn | Cộng thời hạn theo cấu hình và tăng bộ đếm | PASS |
| RD-023 | Gia hạn | Tài khoản khác gia hạn phiếu | Không tìm thấy phiếu trong phạm vi tài khoản | PASS |
| RD-024 | Gia hạn | Phiếu đã trả | Từ chối gia hạn | PASS |
| RD-025 | Gia hạn | Thẻ độc giả không hoạt động | Từ chối gia hạn | PASS |
| RD-026 | Gia hạn | Đã đạt số lần gia hạn cấu hình | Từ chối gia hạn | PASS |
| RD-027 | Gia hạn | Độc giả khác đang đặt giữ đầu sách | Từ chối gia hạn | PASS |
| RD-028 | Tra cứu | Danh sách sách công khai | Chỉ trả sách hoạt động và không tracking entity | PASS |
| RD-029 | Tra cứu | Sách không có ảnh bìa | Trả trạng thái không có ảnh an toàn | PASS |
| RD-030 | Tra cứu | Sách có ảnh bìa | Trả byte ảnh và content type tương ứng | PASS |
| RD-031 | Thông báo | Lấy hộp thư của một tài khoản | Chỉ có dữ liệu của tài khoản đó, mới nhất trước | PASS |
| RD-032 | Thông báo | Đếm chưa đọc | Chỉ đếm thông báo chưa đọc của chủ tài khoản | PASS |
| RD-033 | Thông báo | Đánh dấu một thông báo | Cập nhật trạng thái và thời điểm đọc | PASS |
| RD-034 | Thông báo | Đánh dấu lại thông báo đã đọc | Thao tác idempotent, không đổi thời điểm | PASS |
| RD-035 | Thông báo | Tài khoản khác đánh dấu thông báo | Không tiết lộ và không cập nhật dữ liệu | PASS |
| RD-036 | Thông báo | Đánh dấu tất cả | Chỉ cập nhật thông báo chưa đọc thuộc tài khoản | PASS |
| RD-037 | Thông báo | Không có thông báo chưa đọc | Trả số lượng bằng 0 | PASS |
| RD-038 | Nhắc hạn | Mốc còn 3 ngày và 1 ngày | Tạo đúng loại nhắc hạn | PASS |
| RD-039 | Nhắc hạn | Khoản mượn quá hạn | Thông báo có số ngày trễ | PASS |
| RD-040 | Nhắc hạn | Phiếu đã trả hoặc ngoài mốc | Không phát sinh thông báo nhầm | PASS |
| RD-041 | Nhắc hạn | Chạy lại cùng mốc | Không tạo thông báo trùng | PASS |
| RD-042 | Nhắc hạn | Khoản quá hạn nhiều ngày | Tối đa một thông báo mỗi ngày theo khóa dedupe | PASS |
| RD-043 | Outbox | Sự kiện đặt giữ được hỗ trợ | Chuyển thành thông báo đúng loại | PASS |
| RD-044 | Outbox | Phiếu đặt giữ không còn | Đánh dấu xử lý để tránh retry vô hạn | PASS |
| RD-045 | Outbox | Thông báo đã tồn tại | Xử lý idempotent | PASS |
| RD-046 | Outbox | Event không hỗ trợ | Tăng Attempts và lưu lỗi giới hạn độ dài | PASS |
| RD-047 | Outbox | Hàng đợi lớn hơn giới hạn | Mỗi lượt chỉ xử lý tối đa 100 item | PASS |
| RD-048 | Phân quyền | Controller độc giả | Khai báo policy Borrower theo hợp đồng | PASS |
| RD-049 | API | Route/verb hiện tại | Không đổi route công khai | PASS |
| RD-050 | Schema | Ràng buộc thẻ, hạn trả, số lần gia hạn | Metadata EF giữ đúng check constraint | PASS |
| RD-051 | Schema | Index lịch sử, hàng đợi, thông báo | Metadata EF có đúng thứ tự cột truy vấn | PASS |

## Cách chạy

Chạy toàn solution từ thư mục chứa `LiMS.sln`:

```powershell
dotnet test LiMS.sln --no-restore --verbosity minimal -p:BaseOutputPath=.build-test/
```

`BaseOutputPath` tách thư mục output kiểm thử khỏi output của các ứng dụng đang chạy. Có thể bỏ tham số này nếu không có tiến trình đang giữ file trong `bin/`.

## Kết quả trong phiên cập nhật

- `dotnet test LiMS.Tests/LiMS.Tests.csproj --no-restore -p:BaseOutputPath=.build-test/`: **254 passed, 0 failed, 0 skipped**.
- Các test xác thực metadata kiểm tra model EF; chúng không thực thi check constraint trên SQL Server.
- Các test EF InMemory bỏ qua semantics transaction của SQL Server. Vì vậy kết quả không chứng minh được hành vi khóa `SERIALIZABLE`, deadlock hoặc race condition trên SQL Server thật.
- Script SQL chưa được chạy trên instance SQL Server trong phiên này; kết quả PASS ở ma trận không đại diện cho việc triển khai database.

## Kết luận kiểm thử

Phân hệ độc giả có test cho chuẩn hóa hồ sơ, phạm vi sở hữu dữ liệu, điều kiện đặt giữ/gia hạn, thông báo và outbox. Ma trận trên phản ánh hành vi có test ở cấp service/contract. Trước khi triển khai production cần chạy migration trên bản sao database, kiểm tra dữ liệu legacy vi phạm constraint và kiểm thử concurrency trên SQL Server cùng phiên bản vận hành.
