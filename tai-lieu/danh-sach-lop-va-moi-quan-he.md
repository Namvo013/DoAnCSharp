# DANH SÁCH LỚP THỰC THỂ VÀ CÁC MỐI QUAN HỆ

## 1. Phạm vi

Tài liệu này được lập từ mã nguồn trong `LiMS.Api/Models/LibraryEntities.cs` và cấu hình ánh xạ quan hệ trong `LiMS.Api/Data/LiMSDbContext.cs` của phiên bản dự án được cung cấp.

Danh sách chính tập trung vào 13 lớp thực thể được khai báo dưới dạng `DbSet` trong `LiMSDbContext`. Các lớp hằng số, lớp trao đổi dữ liệu, dịch vụ và bộ điều khiển được phân biệt riêng, không đánh đồng với lớp thực thể cơ sở dữ liệu.

## 2. Danh sách 13 lớp thực thể

| STT | Tên lớp | Vai trò |
|---:|---|---|
| 1 | `Account` | Lưu tài khoản, tên đăng nhập, mật khẩu đã băm, email, họ tên, số điện thoại, vai trò và trạng thái |
| 2 | `Reader` | Lưu hồ sơ độc giả, mã thẻ, ngày cấp, ngày hết hạn, trạng thái thẻ và giới hạn mượn |
| 3 | `Librarian` | Lưu hồ sơ thủ thư và mã nhân viên |
| 4 | `ActivityLog` | Ghi nhận thao tác của tài khoản, đối tượng bị tác động và thời điểm thực hiện |
| 5 | `SystemConfig` | Lưu khóa cấu hình, giá trị cấu hình, mô tả và thời điểm cập nhật |
| 6 | `Category` | Quản lý thể loại sách |
| 7 | `Book` | Lưu thông tin đầu sách như tiêu đề, tác giả, ISBN, nhà xuất bản, năm xuất bản và vị trí kệ |
| 8 | `BookCopy` | Quản lý từng bản sao vật lý của đầu sách, gồm mã vạch và trạng thái |
| 9 | `Reservation` | Lưu yêu cầu đặt trước sách của độc giả, thời điểm tạo, thời điểm hết hạn và trạng thái |
| 10 | `Loan` | Lưu thông tin mượn sách, độc giả, bản sao, thủ thư, ngày mượn, hạn trả, ngày trả và số lần gia hạn |
| 11 | `Fine` | Lưu khoản phạt, số tiền, lý do, trạng thái và thủ thư thu phạt nếu có |
| 12 | `PurchaseProposal` | Lưu đề xuất bổ sung sách của độc giả và thông tin người xét duyệt |
| 13 | `EmailLog` | Ghi nhận lịch sử gửi thư điện tử, loại thư, trạng thái gửi và liên kết nghiệp vụ liên quan |

## 3. Các mối quan hệ được cấu hình trong mã nguồn

| STT | Lớp thứ nhất | Quan hệ | Lớp thứ hai | Căn cứ nghiệp vụ |
|---:|---|---|---|---|
| 1 | `Account` | Một - một | `Reader` | `Reader.AccountId` là khóa ngoại có chỉ mục duy nhất |
| 2 | `Account` | Một - một | `Librarian` | `Librarian.AccountId` là khóa ngoại có chỉ mục duy nhất |
| 3 | `Account` | Một - nhiều | `ActivityLog` | Một tài khoản có thể có nhiều bản ghi nhật ký |
| 4 | `Category` | Một - nhiều | `Book` | Mỗi đầu sách thuộc một thể loại; một thể loại có nhiều đầu sách |
| 5 | `Book` | Một - nhiều | `BookCopy` | Một đầu sách có thể có nhiều bản sao vật lý |
| 6 | `Reader` | Một - nhiều | `Reservation` | Một độc giả có thể tạo nhiều yêu cầu đặt trước |
| 7 | `Book` | Một - nhiều | `Reservation` | Một đầu sách có thể xuất hiện trong nhiều yêu cầu đặt trước |
| 8 | `Reader` | Một - nhiều | `Loan` | Một độc giả có thể có nhiều phiếu mượn |
| 9 | `BookCopy` | Một - nhiều | `Loan` | Một bản sao có thể được mượn nhiều lần ở các thời điểm khác nhau |
| 10 | `Librarian` | Một - nhiều | `Loan` | Một thủ thư có thể lập nhiều phiếu mượn |
| 11 | `Reservation` | Một - một tùy chọn | `Loan` | `Loan.ReservationId` là khóa ngoại tùy chọn có chỉ mục duy nhất |
| 12 | `Loan` | Một - nhiều | `Fine` | Một phiếu mượn có thể phát sinh nhiều khoản phạt |
| 13 | `Librarian` | Một - nhiều tùy chọn | `Fine` | Một thủ thư có thể thu nhiều khoản phạt; khoản phạt có thể chưa được thu |
| 14 | `Reader` | Một - nhiều | `PurchaseProposal` | Một độc giả có thể gửi nhiều đề xuất |
| 15 | `Librarian` | Một - nhiều tùy chọn | `PurchaseProposal` | Một thủ thư có thể xét duyệt nhiều đề xuất; đề xuất có thể chưa được xét duyệt |
| 16 | `Reader` | Một - nhiều | `EmailLog` | Một độc giả có thể có nhiều bản ghi gửi thư |
| 17 | `Loan` | Một - nhiều tùy chọn | `EmailLog` | Bản ghi gửi thư có thể liên quan đến một phiếu mượn |
| 18 | `Reservation` | Một - nhiều tùy chọn | `EmailLog` | Bản ghi gửi thư có thể liên quan đến một yêu cầu đặt trước |

**Lưu ý:** Quan hệ tùy chọn nghĩa là khóa ngoại có thể nhận giá trị rỗng ở phía phụ thuộc. Ví dụ `Loan.ReservationId`, `Fine.CollectedBy`, `PurchaseProposal.ReviewedBy`, `EmailLog.LoanId` và `EmailLog.ReservationId`.

## 4. Nhóm lớp theo nghiệp vụ

### 4.1. Tài khoản và quản trị

- `Account`
- `Reader`
- `Librarian`
- `ActivityLog`
- `SystemConfig`

### 4.2. Danh mục và kho sách

- `Category`
- `Book`
- `BookCopy`

### 4.3. Mượn, trả và đặt trước

- `Reservation`
- `Loan`

### 4.4. Phí phạt, đề xuất và thông báo

- `Fine`
- `PurchaseProposal`
- `EmailLog`

## 5. Các kiểu lớp khác trong mã nguồn

Ngoài 13 lớp thực thể, dự án còn có những lớp phục vụ các nhiệm vụ khác:

- **Lớp hằng số:** `AccountRoles`, `RecordStatuses`.
- **Lớp hợp đồng trao đổi dữ liệu:** các kiểu yêu cầu và phản hồi trong `LiMS.Api/Contracts/ApiContracts.cs`.
- **Lớp truy cập dữ liệu:** `LiMSDbContext`.
- **Lớp dịch vụ:** các lớp trong `LiMS.Api/Services/`.
- **Lớp điều khiển:** các lớp trong `LiMS.Api/Controllers/`.
- **Lớp giao diện và mô hình giao diện:** các lớp trong `LiMS.Admin/` và `LiMS.Web/`.

Các nhóm này không được cộng vào danh sách 13 lớp thực thể ở mục 2 vì chúng không phải cùng một loại lớp dữ liệu.

## 6. Gợi ý sử dụng tài liệu

Khi vẽ sơ đồ lớp miền nghiệp vụ, có thể bắt đầu từ 13 lớp thực thể và các quan hệ tại mục 3. Khi vẽ sơ đồ lớp thiết kế chi tiết, cần đọc thêm thuộc tính và phương thức của từng lớp, đồng thời bổ sung các lớp dịch vụ, bộ điều khiển và lớp trao đổi dữ liệu nếu phạm vi sơ đồ yêu cầu.

Tài liệu này là bản kiểm kê lớp và quan hệ dựa trên mã nguồn; không thay thế sơ đồ UML chi tiết có đầy đủ kiểu dữ liệu, phạm vi truy cập, phương thức và bội số ở hai đầu liên kết.
