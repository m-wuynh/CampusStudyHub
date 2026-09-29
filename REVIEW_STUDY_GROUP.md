# Kịch bản review feature Study Group

## 1. Mục tiêu buổi review

Feature Study Group cho phép sinh viên tìm và tham gia nhóm học tập, tạo nhóm mới,
quản lý thành viên, trao đổi trong nhóm và chia sẻ tài nguyên. Kịch bản chính mất
khoảng 12-15 phút, sử dụng hai tài khoản để thể hiện rõ phân quyền.

## 2. Chuẩn bị trước khi review

- Chạy SQL Server và bảo đảm database `StudyHub` đã có dữ liệu môn học THPT.
- Chạy ứng dụng bằng profile HTTPS:

  ```powershell
  dotnet run --project StudyHub.Web/StudyHub.Web.csproj --launch-profile https
  ```

- Mở `https://localhost:7054` trên hai trình duyệt hoặc một cửa sổ thường và một
  cửa sổ ẩn danh.
- Đăng nhập hai tài khoản Google khác nhau:
  - **Tài khoản A:** người tạo nhóm, sau đó là Owner.
  - **Tài khoản B:** sinh viên muốn tham gia nhóm.
- Chuẩn bị dữ liệu tạo nhóm:

  | Trường | Giá trị demo |
  |---|---|
  | Tên nhóm | Ôn thi Toán 12 - Khối A |
  | Môn học | Toán |
  | Mô tả | Cùng luyện đề và chữa các dạng toán trọng tâm THPT. |
  | Mục tiêu | Hoàn thành 2 đề mỗi tuần, mục tiêu 8+ điểm. |
  | Hình thức | Hybrid |
  | Lịch học | 19:30 thứ 3 và thứ 7 |
  | Liên hệ | https://meet.google.com/ |
  | Nội quy | Tôn trọng thành viên, gửi bài đúng chủ đề. |
  | Số thành viên tối đa | 20 |
  | Cách tham gia | Công khai, cần duyệt |

## 3. Kịch bản trình bày chính

### Bước 1 - Giới thiệu danh sách và tìm kiếm nhóm

**Thao tác**

1. Dùng tài khoản A mở menu **Nhóm học tập**.
2. Giới thiệu ba tab **Nhóm của tôi**, **Đang chờ duyệt**, **Khám phá**.
3. Nhập từ khóa tên nhóm hoặc môn học, ví dụ `Toán`.
4. Xóa từ khóa tìm kiếm.

**Lời trình bày gợi ý**

> Danh sách được lấy trực tiếp từ database theo tài khoản đang đăng nhập. Tìm kiếm
> hỗ trợ tên nhóm, môn học, mô tả và mục tiêu. Nhóm riêng tư chỉ xuất hiện với
> thành viên hoặc người đang có yêu cầu hợp lệ.

**Kết quả mong đợi**

- Danh sách thay đổi sau khoảng 350 ms khi nhập từ khóa.
- URL cập nhật tham số `q` và vẫn giữ được từ khóa khi tải lại trang.
- Nhóm đang tham gia, đang chờ và nhóm có thể khám phá nằm đúng tab.

### Bước 2 - Tạo nhóm mới

**Thao tác**

1. Tài khoản A chọn **Tạo nhóm**.
2. Nhập bộ dữ liệu demo ở phần chuẩn bị.
3. Chọn chế độ **Công khai · Cần duyệt** và tạo nhóm.

**Lời trình bày gợi ý**

> Khi tạo nhóm, hệ thống kiểm tra dữ liệu ở BLL, tạo môn học nếu chưa tồn tại và
> lưu nhóm cùng membership của người tạo trong một transaction. Người tạo được
> xác định là Owner và trở thành thành viên Active ngay lập tức.

**Kết quả mong đợi**

- Hệ thống chuyển đến trang chi tiết nhóm vừa tạo.
- Tài khoản A hiển thị là **Owner**.
- Nhóm hiển thị đúng môn học, mục tiêu, lịch học, hình thức và giới hạn thành viên.
- Owner nhìn thấy các chức năng đăng thông báo, tạo lời mời và quản lý thành viên.

### Bước 3 - Gửi yêu cầu và duyệt thành viên

**Thao tác**

1. Chuyển sang tài khoản B, tìm nhóm `Ôn thi Toán 12 - Khối A`.
2. Mở nhóm và chọn **Gửi yêu cầu**.
3. Kiểm tra nhóm chuyển sang tab **Đang chờ duyệt**.
4. Quay lại tài khoản A, tải lại trang chi tiết và mở tab **Thành viên**.
5. Chọn **Duyệt** đối với tài khoản B.
6. Tài khoản B tải lại trang nhóm.

**Lời trình bày gợi ý**

> Nhóm Approval tạo membership ở trạng thái Pending. Người đang chờ chỉ xem được
> thông tin giới thiệu, chưa xem chat, tài nguyên hoặc danh sách thành viên. Owner
> và Moderator có thể duyệt hoặc từ chối yêu cầu.

**Kết quả mong đợi**

- Trước khi duyệt, B có trạng thái `Pending` và có thể rút yêu cầu.
- Sau khi A duyệt, B chuyển thành `Active` và số thành viên tăng một.
- B xem được các tab Thông báo, Chat, Tài nguyên và Thành viên.

### Bước 4 - Kiểm tra chat thời gian thực

**Thao tác**

1. Giữ trang nhóm mở đồng thời ở cả hai tài khoản.
2. Mở tab **Chat** ở hai cửa sổ.
3. Tài khoản B gửi: `Em đã hoàn thành đề số 1, mọi người cùng chữa câu 42 nhé.`
4. Tài khoản A trả lời: `Tối nay nhóm sẽ chữa câu này lúc 19:30.`

**Lời trình bày gợi ý**

> Chat sử dụng SignalR và chỉ cho thành viên Active vào channel của nhóm. Tin nhắn
> được lưu xuống database trước khi phát đến các thành viên. Nếu SignalR không kết
> nối được, giao diện chuyển sang REST và polling dự phòng.

**Kết quả mong đợi**

- Tin nhắn xuất hiện ở cửa sổ còn lại mà không cần tải lại trang.
- Tên, ảnh đại diện và thời gian gửi hiển thị đúng.
- Tải lại trang vẫn thấy tin nhắn vì dữ liệu đã được lưu trong database.

### Bước 5 - Đăng và ghim thông báo

**Thao tác**

1. Tài khoản A mở tab **Thông báo**.
2. Nhập `Thứ 7 tuần này chữa đề khảo sát số 2 lúc 19:30.`.
3. Chọn **Ghim thông báo** và đăng.
4. Tài khoản B tải lại tab Thông báo.

**Kết quả mong đợi**

- Thông báo ghim nằm trước các thông báo thường.
- B đọc được thông báo nhưng không có form đăng thông báo khi chỉ mang vai trò Member.
- Khi ghim một thông báo mới, thông báo ghim cũ tự được bỏ ghim.

### Bước 6 - Kiểm tra vai trò Moderator

**Thao tác**

1. Tài khoản A mở tab **Thành viên**.
2. Chọn tài khoản B và thực hiện **Đặt làm Moderator**.
3. Tài khoản B tải lại trang.
4. Kiểm tra B có thể đăng thông báo, tạo lời mời và xử lý thành viên.
5. Tài khoản A chọn **Hạ xuống thành viên** để đưa B về Member.

**Kết quả mong đợi**

- Chỉ Owner có thể bổ nhiệm hoặc hạ Moderator.
- Moderator quản lý được Member và yêu cầu Pending.
- Moderator không thể quản lý Owner hoặc một Moderator khác.

### Hạn chế hiện tại - Tài nguyên nhóm

Tab **Tài nguyên** hiện chỉ đọc Note, Document và Flashcard Deck đã có
`StudyGroupId` của nhóm và `Visibility = 'Group'`. Giao diện và API chưa có thao tác
gắn hoặc chia sẻ một tài nguyên vào nhóm; Note, PDF và Flashcard mới đều đang được
tạo ở chế độ `Private`.

Vì vậy không trình bày thao tác thêm tài nguyên như một luồng đã hoàn thiện. Nếu
database đã có sẵn dữ liệu Group do seed hoặc chuẩn bị riêng, chỉ review phần hiển
thị: thành viên Active thấy tài nguyên, còn người ngoài và người Pending không nhận
dữ liệu nội bộ. Đây là phần cần phát triển tiếp để hoàn chỉnh feature.

### Bước 7 - Tạo và chấp nhận lời mời

**Thao tác**

1. Tài khoản A tạo thêm một nhóm với chế độ **Riêng tư · Chỉ qua lời mời**.
2. Trong trang chi tiết, chọn **Tạo lời mời** và sao chép liên kết.
3. Mở liên kết bằng tài khoản B.
4. Kiểm tra tên nhóm, môn học, thời hạn và số lượt còn lại.
5. Chọn **Tham gia nhóm**.

**Lời trình bày gợi ý**

> Mã mời được sinh ngẫu nhiên, có ngày hết hạn và giới hạn lượt dùng. Với nhóm
> InviteOnly, người ngoài không thể tìm thấy hoặc tự gửi yêu cầu tham gia.

**Kết quả mong đợi**

- B không tìm thấy nhóm riêng tư trước khi dùng liên kết.
- Lời mời hợp lệ đưa B vào nhóm ở trạng thái Active, không cần chờ duyệt.
- Số lượt đã dùng của lời mời tăng lên.

### Bước 8 - Rời nhóm và quy tắc bảo vệ

**Thao tác**

1. Tài khoản B chọn **Rời nhóm**.
2. Tài khoản A thử rời nhóm do chính mình tạo.

**Kết quả mong đợi**

- B rời nhóm thành công và mất quyền truy cập chat/nội dung nội bộ.
- Owner không thể rời nhóm; hệ thống yêu cầu chuyển quyền hoặc lưu trữ nhóm trước.

## 4. Các kiểm thử biên nên review

| Mã | Tình huống | Kết quả mong đợi |
|---|---|---|
| SG-01 | Tạo nhóm bỏ trống tên | Báo tên nhóm là bắt buộc, không tạo dữ liệu |
| SG-02 | Nhập URL liên hệ không phải HTTP/HTTPS/mailto | Báo liên kết không hợp lệ |
| SG-03 | Nhập số thành viên ngoài 2-500 | Hệ thống dùng giá trị mặc định 20 |
| SG-04 | Member đăng announcement qua API | Trả về 403 |
| SG-05 | Người ngoài gửi chat qua Hub/API | Bị từ chối vì chưa phải Active member |
| SG-06 | Nhóm Open | Người dùng tham gia và trở thành Active ngay |
| SG-07 | Nhóm Approval | Người dùng trở thành Pending cho đến khi được duyệt |
| SG-08 | Nhóm InviteOnly | Nút tự tham gia bị khóa; chỉ link mời hợp lệ được chấp nhận |
| SG-09 | Nhóm đủ `MaxMembers` | Trả về 409 và không thêm thành viên |
| SG-10 | Người đã bị ban tham gia lại | Trả về 403 |
| SG-11 | Lời mời hết hạn hoặc hết lượt | Trang mời không còn truy cập được và không thêm membership |
| SG-12 | Owner kick/ban chính mình | Thao tác bị từ chối |
| SG-13 | Tìm kiếm bằng tên môn học | Trả đúng nhóm có môn tương ứng |
| SG-14 | Nội dung chat rỗng hoặc quá 1000 ký tự | Không tạo tin nhắn |
| SG-15 | Thông báo quá 4000 ký tự | Không tạo thông báo |

## 5. Nội dung giải thích kiến trúc khi được hỏi

Luồng xử lý chính:

```text
Razor View + JavaScript
        |
StudyGroupsController / StudyGroupsApiController / StudyGroupHub
        |
IStudyGroupService -> StudyGroupService
        |
IRepository.StudyGroups -> SqlStudyGroupRepository
        |
SQL Server
```

- **Presentation:** controller trả Razor View; API phục vụ các thao tác động; SignalR
  Hub phục vụ chat thời gian thực.
- **BLL:** `StudyGroupService` kiểm tra dữ liệu đầu vào, phân loại lỗi nghiệp vụ và
  ánh xạ dữ liệu DAL sang DTO.
- **DAL:** `SqlStudyGroupRepository` truy vấn EF Core, kiểm tra quyền trên dữ liệu
  thật và dùng transaction cho các thao tác cần tính nhất quán.
- **Bảo mật:** toàn bộ controller và Hub yêu cầu đăng nhập; thao tác thay đổi dữ liệu
  dùng antiforgery token; quyền được kiểm tra lại ở server.
- **Dữ liệu:** trạng thái thành viên gồm `Pending`, `Active`, `Rejected`, `Left`,
  `Banned`; vai trò quản lý gồm Owner và Moderator.

## 6. Tiêu chí kết thúc review

Feature được xem là đạt khi:

- Dữ liệu danh sách và tìm kiếm phản ánh đúng database.
- Ba chế độ `Open`, `Approval`, `InviteOnly` hoạt động đúng.
- Phân quyền Owner, Moderator, Member và người ngoài không bị vượt qua bằng API.
- Chat cập nhật trên hai trình duyệt và vẫn tồn tại sau khi tải lại.
- Thông báo, lời mời và quản lý thành viên cho kết quả đúng.
- Phần Tài nguyên được ghi nhận là chỉ có chức năng hiển thị cho đến khi bổ sung
  luồng chia sẻ tài nguyên vào nhóm.
- Các lỗi nghiệp vụ trả thông báo dễ hiểu và không tạo dữ liệu dở dang.

