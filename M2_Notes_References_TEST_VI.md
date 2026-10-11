# Kiểm thử M2 — Ghi chú và tham chiếu PDF

## Chuẩn bị

1. Giải nén ZIP và mở thư mục `CampusStudyHub`.
2. Trong SQL Server Management Studio, chọn database mà `StudyHub.Web/appsettings.Development.json` đang dùng.
3. Nếu database chưa có M1, chạy `Data/StudyHub_SQLServer/06_M1_NotesPdf.sql` trước.
4. Chạy `Data/StudyHub_SQLServer/07_M2_NotesReferences.sql`.
5. Chạy ứng dụng:

   ```powershell
   dotnet run --project StudyHub.Web/StudyHub.Web.csproj --launch-profile https
   ```

   ZIP đã có sẵn JavaScript/CSS đã build; không cần cài Node.js hay chạy `npm ci` để kiểm thử.

## Luồng ghi chú

1. Đăng nhập phát triển, mở **Ghi chú & Tài liệu**.
2. Chọn **Tạo ghi chú**, nhập nội dung, thử tiêu đề, chữ đậm/nghiêng, danh sách và liên kết.
3. Nhấn **Lưu** hoặc Ctrl+S, tải lại trang và xác nhận Delta cùng tiêu đề được giữ nguyên.
4. Tạo thêm PDF nếu thư viện chưa có tài liệu; quay lại ghi chú, chọn PDF rồi nhấn **Mở cạnh PDF**.

## Luồng PDF → Note → PDF

1. Trong Reader, nhấn **Ghi chú cạnh PDF**, chọn một ghi chú có thể sửa hoặc tạo ghi chú mới.
2. Nhấn **Chụp chữ**, kéo chọn một cụm chữ trên trang PDF và xác nhận chữ được chèn vào Note.
3. Kiểm tra danh sách **Tham chiếu PDF** có trang và tên tài liệu tương ứng.
4. Nhấn tham chiếu vừa tạo; Reader phải mở đúng phiên bản PDF, trang và vùng đã chọn.
5. Nhấn **Chụp vùng**, kéo một vùng có chữ/ảnh trên trang; xác nhận ảnh vùng đó xuất hiện trong Note và tham chiếu mở lại đúng vùng.
6. Lưu ghi chú, tải lại Reader, chọn lại ghi chú và xác nhận nội dung cùng tham chiếu vẫn còn.
7. Mở cùng ghi chú ở hai tab, lưu nội dung ở tab thứ nhất rồi thử lưu bản cũ ở tab thứ hai. Tab thứ hai phải báo xung đột và giữ nội dung đang sửa trên màn hình.

## Lưu trữ

1. Từ thư viện, lưu trữ một ghi chú.
2. Mở ghi chú đã lưu trữ: nội dung ở chế độ chỉ đọc và không thể chụp thêm.
3. Khôi phục ghi chú rồi xác nhận có thể sửa lại.

Mỗi tham chiếu lưu `DocumentVersionId`, số trang, loại capture và tọa độ PDF; quyền đọc được kiểm tra lại khi mở tài liệu. Capture và cập nhật Delta được lưu trong cùng một lần ghi database để tránh tạo tham chiếu mồ côi.
