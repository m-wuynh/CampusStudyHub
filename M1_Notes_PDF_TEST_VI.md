# Kiểm thử M1 — Thư viện PDF và Reader

## Chuẩn bị một lần

1. Giải nén ZIP và mở thư mục `CampusStudyHub`.
2. Mở SQL Server Management Studio, chọn đúng database mà connection string `StudyHub` trong `StudyHub.Web/appsettings.Development.json` đang trỏ tới.
3. Mở và chạy `Data/StudyHub_SQLServer/06_M1_NotesPdf.sql`. Script thêm cột/bảng M1, không xóa dữ liệu hiện có.
4. Trong terminal, chuyển tới thư mục `CampusStudyHub` rồi chạy:

   ```powershell
   dotnet run --project StudyHub.Web/StudyHub.Web.csproj --launch-profile https
   ```

5. Mở `https://localhost:7054`, đăng nhập bằng nút **Đăng nhập phát triển**.

## Luồng nghiệm thu

1. Mở **Ghi chú & Tài liệu** ở menu.
2. Chọn PDF nhỏ hơn 25 MB; xác nhận thanh tiến độ chạy tới 100%, rồi tài liệu xuất hiện trong thư viện.
3. Mở tài liệu. Xác nhận trang đầu được render và Reader hiển thị số trang.
4. Thử điều hướng trang, tìm một cụm chữ, phóng to/thu nhỏ và vừa chiều rộng.
5. Thử **Tô sáng vùng chọn**, **Bút**, **Khung** và **Bình luận**.
6. Tạo annotation, lưu bằng nút **Lưu**. Tạo thay đổi khác rồi dùng Ctrl+S; trạng thái phải báo đã lưu.
7. Tải lại Reader. Xác nhận annotation vẫn đúng vị trí và trạng thái là đã lưu.
8. Quay lại thư viện, thử tìm kiếm, sắp xếp, ghim/bỏ ghim, đổi tên và lưu trữ/khôi phục.
9. Mở PDF đã lưu trữ. Xác nhận Reader ở chế độ chỉ đọc. Xóa một tài liệu thử để kiểm tra xóa mềm.

## Ghi chú

- Upload hiện lưu file vào `StudyHub.Web/App_Data/PdfFiles` trên máy chạy ứng dụng; giữ thư mục này khi sao lưu/mang dữ liệu sang máy khác.
- Nếu muốn build lại JavaScript, chạy `npm ci` rồi `npm run build` trong `StudyHub.Web/Notes`. Máy chỉ nghiệm thu ứng dụng không cần Node.js vì bundle đã có sẵn trong ZIP.
- Môi trường đóng gói không có .NET SDK hoặc SQL Server, vì vậy cần chạy nghiệm thu ứng dụng và script SQL trên máy phát triển.
