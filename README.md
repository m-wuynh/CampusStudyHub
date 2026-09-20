<div align="center">
<img width="1200" height="475" alt="GHBanner" src="https://ai.google.dev/static/site-assets/images/share-ais-513315318.png" />
</div>

# Campus Study Hub

Ứng dụng React/Vite hỗ trợ quản lý ghi chú, flashcards, lịch học, điểm số và nhóm học tập.

## Feature nhóm học tập

- Tạo, sửa và xóa nhóm; cấu hình môn học, mục tiêu, sức chứa, hình thức, lịch và liên kết liên hệ.
- Tìm kiếm, lọc theo môn; xem nhóm đã tham gia, khám phá nhóm công khai và lưu nhóm yêu thích.
- Sinh viên gửi hoặc hủy yêu cầu tham gia; trưởng nhóm duyệt hoặc từ chối; kiểm soát nhóm đã đủ người.
- Nội dung bảng tin, thành viên, ghi chú và flashcards chỉ hiển thị cho thành viên.
- Thành viên đăng thông báo, chia sẻ ghi chú/flashcards và rời nhóm.
- Chat box riêng cho từng nhóm: chỉ thành viên được đọc/gửi, hỗ trợ Enter để gửi, Shift + Enter để xuống dòng, tự cuộn tới tin mới và đồng bộ giữa các tab cùng trình duyệt.
- Dữ liệu nhóm và tin nhắn được lưu trong `localStorage` (khóa `study-hub-groups-v3`) để không mất khi tải lại trang. Đây là tầng demo frontend; khi nối backend cần thay `localStorage` bằng API, database và SignalR/WebSocket để nhiều thiết bị nhận tin nhắn theo thời gian thực.

View your app in AI Studio: https://ai.studio/apps/81721623-9e94-4837-ad00-91408623b5ef

## Run Locally

**Prerequisites:**  Node.js


1. Install dependencies:
   `npm install`
2. Nếu dùng tính năng AI, đặt `GEMINI_API_KEY` trong `.env.local`.
3. Chạy ứng dụng:
   `npm run dev`
