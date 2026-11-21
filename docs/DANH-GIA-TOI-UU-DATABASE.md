# Đánh giá schema và tối ưu truy vấn LiMS

## Tóm tắt phạm vi

Phần database tập trung vào schema SQL Server mà `LiMS.Api.Data.LiMSDbContext` mô tả. Các bảng dùng tên viết hoa hiện tại để giữ tương thích với migration và dữ liệu của nhóm. Thay đổi schema được đóng gói bằng migration EF Core; script triển khai thủ công có kiểm tra trước và có thể chạy lặp sau khi migration đã hoàn tất.

Không có thao tác cập nhật tồn kho bằng trigger. Số lượng và trạng thái tồn kho được suy ra từ các dòng `BOOK_COPY`; việc mượn, trả, hủy phiếu đang được quản lý trong service/API và cần giữ cùng transaction với audit/outbox. Một trigger độc lập có thể ghi đè quy tắc này hoặc phát sinh side effect ngoài transaction của ứng dụng.

## Ràng buộc hiện có được xác nhận

| Phạm vi | Cơ chế | Bảo vệ |
|---|---|---|
| Mọi bảng nghiệp vụ | Primary key | Định danh duy nhất cho từng dòng |
| Account và reader | Foreign key, unique | Liên kết hồ sơ với account; username, email, mã thẻ duy nhất |
| Sách | Foreign key, unique/filter | ISBN duy nhất khi có giá trị; đầu sách thuộc thể loại |
| Bản sao | Foreign key, unique | Barcode duy nhất; bản sao thuộc một đầu sách |
| Phiếu mượn | Foreign key, filtered unique | Một bản sao không có hơn một loan chưa trả; reservation gắn một loan |
| Phạt | Check constraint | `Amount >= 0` |
| Notification/outbox | Unique key | Chống tạo trùng theo khóa deduplication |
| Liên kết nghiệp vụ | Foreign key `NoAction` | Ngăn xóa cha làm mất lịch sử kiểm toán/mượn trả |

Unique email/username dựa vào collation của SQL Server hiện tại. Trước khi chuyển sang collation phân biệt hoa thường cần chuẩn hóa dữ liệu và kiểm tra trùng không phân biệt hoa thường.

## Ràng buộc và migration mới

Migration `AddReaderIntegrityAndQueryIndexes` bổ sung:

| Constraint / index | Ý nghĩa |
|---|---|
| `CK_READER_CardDateRange` | Ngày hết hạn thẻ không trước ngày phát hành |
| `CK_READER_BorrowLimit` | Giới hạn mượn không âm |
| `CK_RESERVATION_ExpiryAfterCreated` | Hạn yêu cầu không trước lúc tạo |
| `CK_LOAN_DueDateAfterBorrowDate` | Hạn trả không trước ngày mượn |
| `CK_LOAN_RenewCount` | Số lượt gia hạn không âm |
| `IX_LOAN_ReaderId_ReturnDate_DueDate` | Lịch sử loan theo độc giả, trạng thái trả và hạn trả |
| `IX_RESERVATION_BookId_Status_ExpireAt_CreatedAt` | Lọc lượt theo sách/trạng thái/hạn và lấy theo thứ tự tạo |
| `IX_READER_NOTIFICATION_ReaderId_IsRead_CreatedAt` | Lọc hộp thư theo độc giả/trạng thái và sắp xếp thời gian |

Các check constraint được validate với toàn bộ dữ liệu hiện tại khi migration áp dụng. Nếu dữ liệu cũ vi phạm, migration sẽ dừng thay vì đánh dấu constraint không đáng tin cậy. Hãy chạy truy vấn preflight dưới đây, sửa các dòng vi phạm bằng quy trình nghiệp vụ đã phê duyệt, sau đó mới triển khai.

```sql
SELECT ReaderId, CardNo, CardIssueDate, CardExpiryDate, BorrowLimit
FROM dbo.READER
WHERE CardExpiryDate < CardIssueDate OR BorrowLimit < 0;

SELECT ReservationId, CreatedAt, ExpireAt
FROM dbo.RESERVATION
WHERE ExpireAt < CreatedAt;

SELECT LoanId, BorrowDate, DueDate, RenewCount
FROM dbo.LOAN
WHERE DueDate < CONVERT(date, BorrowDate) OR RenewCount < 0;
```

## Chỉ mục truy vấn

`IX_LOAN_ReaderId_ReturnDate_DueDate` hỗ trợ truy vấn lịch sử một độc giả và lọc loan đang mở/quá hạn. Index này không thay thế khóa ngoại hoặc filtered unique index theo `CopyId` bảo vệ một copy khỏi hai lần mượn đồng thời.

`IX_RESERVATION_BookId_Status_ExpireAt_CreatedAt` tương ứng các điều kiện chọn lượt trong `ReservationQueueService`: một `BookId`, tập trạng thái đang hoạt động, `ExpireAt` hợp lệ, sau đó xếp FIFO bằng `CreatedAt`. Index cũ chỉ theo `BookId` được thay thế bởi composite index do migration scaffold nhận diện prefix.

`IX_READER_NOTIFICATION_ReaderId_IsRead_CreatedAt` phục vụ đếm chưa đọc và danh sách thông báo theo thời gian. Schema vẫn giữ hai index hiện hữu vì chúng phục vụ các hình dạng truy vấn khác nhau; cần theo dõi `sys.dm_db_index_usage_stats` và execution plan trước khi gỡ bất kỳ index nào.

Script `database/indexes_optimization.sql` tạo thêm index có lọc cho outbox chưa xử lý và index bản sao theo trạng thái. Script dừng nếu chưa có schema nền, báo lỗi dữ liệu cũ trước khi áp dụng check constraint, và bỏ qua index/constraint đã tồn tại. Chạy sau EF migration để tránh migration tạo lại index cùng tên.

## View và stored procedure

`database/reader_views_and_procedures.sql` cung cấp các đối tượng đọc:

- `vw_ReaderPopularBooks`: tổng hợp lượt mượn, số bản sao và tình trạng bản sao theo đầu sách.
- `vw_ReaderLoanHistoryDetails`: chi tiết lịch sử, người xử lý và tổng tiền phạt từ các khoản fine.
- `vw_ReaderOverdueLoans`: danh sách các loan chưa trả và đã quá hạn theo UTC hiện tại.
- `vw_ReaderReservationQueue`: thứ tự các yêu cầu còn hiệu lực theo sách.
- `usp_Reader_GetPopularBooks`: giới hạn số dòng trả về từ 1 đến 500.
- `usp_Reader_GetLoanHistory`: phân trang lịch sử của `AccountId` đã cung cấp và trả `TotalCount` ở result set thứ hai.
- `usp_Reader_GetOverdueLoans`: xem danh sách quá hạn tại ngày UTC hoặc ngày đối soát được truyền vào.
- `usp_Reader_GetBookAvailability`: tổng hợp Available/OnLoan/Maintenance từ các bản sao.

Các stored procedure báo cáo không tự xác thực JWT. Nếu ứng dụng gọi trực tiếp, phải truyền AccountId lấy từ principal đã xác thực; không nhận AccountId tùy ý từ trình duyệt. `LiMS.Api` hiện là lớp thực thi authorization và query; các đối tượng SQL này chưa được tích hợp làm đường gọi API.

## Seed dữ liệu kiểm thử

`database/seed_reader_test_data.sql` là fixture idempotent cho database có tên chứa `test` hoặc `dev`. Account mẫu bị vô hiệu hóa và có hash không hợp lệ có chủ đích, vì vậy script không tạo thông tin đăng nhập mặc định. Fixture gồm hai reader, một librarian, hai đầu sách, bản sao ở trạng thái OnLoan/Maintenance/Available, loan đang mở và đã trả, fine và reservation.

Không chạy seed trên production. Dữ liệu test có thể dùng mã ISBN/barcode dành riêng cho fixture; tránh dùng các giá trị này khi nhập dữ liệu nghiệp vụ thật. Có thể xóa fixture theo username, barcode và ISBN có tiền tố `FIXTURE` sau khi kết thúc kiểm thử, nhưng cần kiểm tra các FK trước khi xóa.

## Cơ chế truy vấn và giao dịch ở API

Các truy vấn catalog/hồ sơ/lịch sử/thông báo hiện dùng projection DTO và `AsNoTracking()` khi chỉ đọc. Các truy vấn thống kê số bản sao được viết dưới dạng LINQ projection để EF dịch thành truy vấn SQL tổng hợp thay vì tải collection vào bộ nhớ.

Đặt giữ, hủy lượt và gia hạn dùng transaction `Serializable` trong `ReaderService`; service trả sách/thăng hạng hàng đợi và worker xử lý hết hạn cũng dùng transaction theo đầu sách. Unique filtered index trên loan bảo vệ invariant một bản sao chỉ có một loan chưa trả. Outbox ghi cùng `DbContext` với thao tác nghiệp vụ để worker phát thông báo sau đó.

EF InMemory không mô phỏng khóa hoặc isolation level SQL Server. Test hiện xác minh quy tắc ở service và metadata index/constraint; cần thêm chạy kiểm thử song song trên SQL Server staging để đánh giá tranh chấp thực tế và retry khi deadlock.

## Quy trình áp dụng

1. Sao lưu database và xác nhận nhánh ứng dụng/migration cần phát hành.
2. Chạy các câu preflight ở trên và xử lý dữ liệu vi phạm.
3. Chạy `dotnet ef database update --project LiMS.Api --context LiMSDbContext` trên staging trước.
4. Kiểm tra migration history, check constraints có trạng thái trusted và index có đúng cột.
5. Chạy script `indexes_optimization.sql` để thêm index bổ sung ngoài migration.
6. Tạo view/procedure bằng `reader_views_and_procedures.sql`.
7. Chỉ chạy seed trên database test/dev cô lập.
8. Chạy kiểm thử API trên SQL Server staging, sau đó quan sát thời gian query, logical reads, deadlock và index usage.

## Các giới hạn đã xác nhận

- Chưa có SQL Server instance được cấu hình để chạy migration/script trực tiếp trong phiên cập nhật.
- Không thực hiện benchmark tải; không có cơ sở để khẳng định phần trăm cải thiện latency.
- Không đổi response hoặc route hiện hữu để thêm pagination vào API list; các stored procedure phân trang là tiện ích SQL riêng, chưa thay thế contract API.
- Không tạo trigger đồng bộ trạng thái bản sao. Các cập nhật mượn/trả phải tiếp tục đi qua service để giữ transaction, audit và notification outbox.
- Cần kiểm thử concurrency trên SQL Server thật; kết quả EF InMemory không đủ để chứng minh không có race condition.
