# M3 + M4 — smoke test StudyGroup và nghiệm thu

## Chạy đúng solution

1. Mở SQL Server Management Studio và kết nối đúng database mà `StudyHub.Web/appsettings*.json` trỏ tới.
2. Chạy lần lượt toàn bộ script trong thư mục `Data/StudyHub_SQLServer`:
   - `06_M1_NotesPdf.sql`
   - `07_M2_NotesReferences.sql`
   - `08_M3_GroupApproval.sql`
   - `09_M3_M4_Preflight.sql`

   Script cuối phải trả `M3/M4 schema preflight: PASS`. Nó kiểm tra cả kiểu `CapturedText = nvarchar(max)` để tránh lỗi `nvarchar(12000)` cũ.
3. Mở CMD tại thư mục solution rồi chạy:

   ```bat
   dotnet build StudyHub.Web\StudyHub.Web.csproj
   dotnet run --project StudyHub.Web\StudyHub.Web.csproj --launch-profile https
   ```

4. Đăng nhập và vào `/Notes`. M0 demo fake vẫn ở `/phase0/group-gateway-checks`; M3/M4 không dùng fake đó.

## Chuẩn bị StudyGroup smoke test

- Không chạy `02_SeedDemo.sql`: script đó xóa dữ liệu nghiệp vụ và tạo lại user demo. Mở `/Notes/DevelopmentRoles` (chỉ có ở Development). Nếu database có cặp quyền thật, trang dùng Owner và thành viên Active có sẵn. Nếu chỉ có một tài khoản, bấm **Tạo nhóm smoke test trong Notes**; Notes tạo một group phát triển qua StudyGroup service và cho chuyển giữa Owner thật với nhánh Member giả lập bằng cùng UserId. Không tạo user hay membership giả. Cách này kiểm tra luồng UI/API và ChangeRequest, nhưng không thay thế kiểm thử membership với hai UserId khác nhau.
- Selector chọn cặp đầu tiên theo `StudyGroupId`, gồm Owner Active và một thành viên Active khác. Dùng truy vấn chỉ đọc này để xem cặp và nhóm mà selector sẽ dùng:

  ```sql
  SELECT TOP (1) g.StudyGroupId, g.GroupName,
         owner.UserId AS OwnerUserId, owner.DisplayName AS OwnerName,
         member.UserId AS MemberUserId, member.DisplayName AS MemberName
  FROM dbo.StudyGroups g
  JOIN dbo.Users owner ON owner.UserId = g.OwnerUserId
  JOIN dbo.GroupMembers gm ON gm.StudyGroupId = g.StudyGroupId
  JOIN dbo.Users member ON member.UserId = gm.UserId
  WHERE g.IsArchived = 0 AND owner.Status = 'Active'
    AND gm.Status = 'Active' AND member.Status = 'Active'
    AND member.UserId <> owner.UserId
  ORDER BY g.StudyGroupId, member.UserId;
  ```

- Dùng persona **Chủ nhóm** cho G1. Nếu dùng **Member giả lập**, nó vẫn giữ nguyên UserId của tài khoản hiện tại; chỉ dùng để kiểm tra UI/API Notes chuyển thao tác sang ChangeRequest. Không dùng kết quả này để kết luận đã kiểm thử membership hoặc quyền giữa hai tài khoản thật.

## Smoke test ngay trong StudyGroup

1. Chọn **Chủ nhóm** ở **Kiểm thử Owner/Member** trong `/Notes`, tải lên một PDF nhỏ và tạo ghi chú cá nhân có nội dung `Snapshot ban đầu`.
2. Nhấn **Chia sẻ vào nhóm** trên PDF và ghi chú, chọn G1.
3. Mở `/Groups/{G1}` → tab **Tài nguyên** → **Notes & PDF workspace**. PDF và Group Note phải hiện ở đây.
4. Trong G1 mở PDF, dùng persona Chủ nhóm tô một nét rồi **Lưu annotation nhóm**. Sau đó chuyển sang **Thành viên Active** hoặc **Member giả lập**, mở cùng workspace và xác nhận nội dung được chia sẻ.
5. Chủ nhóm đổi ghi chú cá nhân thành `Bản cá nhân đã đổi`. Group Note trong G1 vẫn phải là `Snapshot ban đầu`.

## ChangeRequest và AuditLog

1. Chọn **Thành viên Active** hoặc **Member giả lập**, vào workspace G1. Sửa Group Note hoặc thêm annotation rồi lưu. Giao diện phải báo đã gửi đề xuất; nội dung chính thức vẫn giữ nguyên.
2. Dùng **Kiểm thử Owner/Member** để trở lại **Chủ nhóm**, mở workspace G1. Chủ nhóm thấy nút **Duyệt/Từ chối** cho đề xuất tương ứng.
3. Từ chối một đề xuất: nội dung chính thức không đổi.
4. Gửi đề xuất mới và duyệt: nội dung chính thức đổi. Kiểm tra SQL:

   ```sql
   SELECT TOP (20) ActionCode, EntityType, EntityKey, Summary, CreatedAtUtc
   FROM dbo.AuditLogs
   WHERE ActionCode LIKE 'ChangeRequest.%'
   ORDER BY AuditLogId DESC;
   ```

   Phải có audit `ChangeRequest.Approved`; status proposal và thay đổi nội dung được ghi cùng transaction.
5. Tạo một đề xuất khác từ persona Thành viên Active hoặc Member giả lập. Chủ nhóm sửa trực tiếp nội dung chính thức trước khi duyệt đề xuất đó. Khi chủ nhóm duyệt phiên bản cũ, API trả HTTP 409, proposal thành `Conflict`, và nội dung mới không bị ghi đè.
6. Chỉ với persona Thành viên Active thật, rời G1 rồi gọi `/api/notes/groups/{G1}/workspace`; request phải bị từ chối. Member giả lập vẫn dùng membership thật của tài khoản hiện tại nên không kiểm tra được thu hồi membership.

## Các điều kiện kiểm tra phụ

- Gọi trực tiếp `PUT /api/notes/groups/{G1}/documents/{documentId}/annotations` bằng persona Thành viên Active hoặc Member giả lập phải bị HTTP 403; luồng UI chỉ tạo ChangeRequest.
- Archive PDF: nhóm vẫn đọc được nhưng viewer ở chế độ chỉ đọc; không share mới và không duyệt proposal lên nội dung Archived.
- Restore PDF: PDF trở về Active + Private; lượt chia sẻ cũ không tự bật lại. Chủ nhóm phải share lại nếu muốn.
- Unshare PDF: request mở workspace hoặc tải file trong nhóm đó bị 404; nhóm khác vẫn truy cập nếu share còn active.
- Nếu membership bị thu hồi, cả workspace, thông tin PDF, HEAD và tải byte-range đều bị chặn ở request tiếp theo.

## Kết quả môi trường hiện tại

- `npm run build` và `node --check` cho các module JS: PASS.
- SQL scripts được đối chiếu kiểu dữ liệu và tên FK/constraint với EF mapping; `CapturedText` dùng `nvarchar(max)`.
- Chưa chạy được `dotnet build`, SQL Server và browser E2E trong môi trường đóng gói này vì không có .NET SDK/SQL Server/browser tương tác. Hãy dùng preflight trước rồi chạy đúng smoke test trên Windows; không nên đánh dấu M3/M4 đã nghiệm thu trước các bước trên.
