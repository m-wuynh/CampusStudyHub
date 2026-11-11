# Business Process Flows - Phân Hệ Calendar & Tích Hợp Mục Tiêu (StudyHub)

Tài liệu đặc tả các luồng nghiệp vụ (Business Process - BP) theo chuẩn **2-lane Swimlane (Student | System)**, thể hiện kiến trúc liên kết thực tế giữa **Lịch học (Calendar)** và **Mục tiêu học tập (Goals & Grades)**.

---

## 1. BP-01 Create or Update Schedule Event (Quản lý & Cập nhật Sự kiện)
*Gộp chung Thêm mới và Chỉnh sửa vào một luồng thống nhất vì dùng chung biểu mẫu và quy tắc kiểm tra thời gian.*

```mermaid
flowchart TD
    subgraph BP01["BP-01 Create or Update Schedule Event"]
        direction TB
        subgraph Student["Student"]
            S1_Start((Start))
            S1_Choice{Action?}
            S1_ClickAdd[Click 'Add Event' button]
            S1_ClickEdit[Click existing event -> Click 'Edit']
            S1_Input[Enter / Modify title, date, start & end time, category, reminders; click 'Save']
            S1_ViewCal[View updated event on calendar]
            S1_End(((●)))
        end

        subgraph System["System"]
            SYS1_EmptyForm[Display empty Create form with default date/time]
            SYS1_PrefillForm[Check if ended/completed: If not, open Update form pre-filled with data]
            SYS1_Validate{End time > Start time AND Start time not in past?}
            SYS1_ShowErr[Show validation error toast]
            SYS1_SaveDB[Save new event OR update existing event & refresh reminders]
            SYS1_Render[Re-render FullCalendar and update weekly progress]
        end
    end

    S1_Start --> S1_Choice
    S1_Choice -- Add New --> S1_ClickAdd
    S1_Choice -- Edit Existing --> S1_ClickEdit

    S1_ClickAdd --> SYS1_EmptyForm
    SYS1_EmptyForm --> S1_Input

    S1_ClickEdit --> SYS1_PrefillForm
    SYS1_PrefillForm --> S1_Input

    S1_Input --> SYS1_Validate

    SYS1_Validate -- Invalid --> SYS1_ShowErr
    SYS1_ShowErr --> S1_Input

    SYS1_Validate -- Valid --> SYS1_SaveDB
    SYS1_SaveDB --> SYS1_Render
    SYS1_Render --> S1_ViewCal
    S1_ViewCal --> S1_End
```

---

## 2. BP-02 Track Progress & Toggle Event Completion (Theo dõi & Đánh dấu Hoàn thành)
*Thể hiện tương tác trên giao diện FullCalendar, cơ chế phản hồi tức thì 0ms (Optimistic UI) và xử lý khi ca học bị quá hạn (Missed).*

```mermaid
flowchart TD
    subgraph BP02["BP-02 Track Progress & Toggle Event Completion"]
        direction TB
        subgraph Student["Student"]
            S2_Start((Start))
            S2_ClickEvent[Click on a scheduled event on Calendar grid]
            S2_ClickToggle[Click 'Mark as completed / Incomplete' button]
            S2_End(((●)))
        end

        subgraph System["System"]
            SYS2_ShowPop[Display event popover with details and reminders]
            SYS2_CheckPast{Event has already ended (past)?}
            SYS2_LockBtn[Disable toggle button with badge 'Missed', hide Edit button]
            SYS2_Optimistic[Immediately update UI 0ms: strike-through title and update weekly progress bar]
            SYS2_SaveDB[Persist new completion status to database in background]
            SYS2_CheckSave{Update success?}
            SYS2_Rollback[Revert UI to previous state and show error toast]
        end
    end

    S2_Start --> S2_ClickEvent
    S2_ClickEvent --> SYS2_ShowPop
    SYS2_ShowPop --> SYS2_CheckPast

    SYS2_CheckPast -- Yes --> SYS2_LockBtn
    SYS2_LockBtn --> S2_End

    SYS2_CheckPast -- No --> S2_ClickToggle
    S2_ClickToggle --> SYS2_Optimistic
    SYS2_Optimistic --> SYS2_SaveDB
    SYS2_SaveDB --> SYS2_CheckSave

    SYS2_CheckSave -- No --> SYS2_Rollback
    SYS2_Rollback --> S2_End

    SYS2_CheckSave -- Yes --> S2_End
```

---

## 3. BP-03 Goal Activity to Calendar Synchronization (Đồng bộ Mục Tiêu & Lịch Học)
*Luồng tích hợp liên phân hệ đắt giá nhất: Tạo hành động từ Mục tiêu điểm số $\rightarrow$ Tự động đồng bộ lên Lịch học $\rightarrow$ Học viên học và tick hoàn thành trên Lịch $\rightarrow$ Phản hồi tiến độ về Mục tiêu.*

```mermaid
flowchart TD
    subgraph BP03["BP-03 Goal Activity to Calendar Synchronization"]
        direction TB
        subgraph Student["Student"]
            S3_Start((Start))
            S3_CreateGoalTask[In 'Grades & Goals' page: Click 'Add Activity' for a specific Goal]
            S3_InputTask[Enter activity title, date & time, description; click 'Add Activity']
            S3_OpenCal[Navigate to Calendar page to study]
            S3_TickDone[Click the Goal activity ca học -> Click 'Mark as completed']
            S3_BackToGoal[Return to 'Grades & Goals' to review progress]
            S3_End(((●)))
        end

        subgraph System["System"]
            SYS3_SaveGoalEvent[Create ScheduleEvent linked with GoalId in database]
            SYS3_SyncCal[Automatically render the activity on FullCalendar grid with linked Goal tag]
            SYS3_UpdateCompletion[Update IsCompleted = true, update weekly progress bar 0ms]
            SYS3_SyncGoalView[Display the activity as 'Done' in Goal ladder, contributing to goal achievement]
        end
    end

    S3_Start --> S3_CreateGoalTask
    S3_CreateGoalTask --> S3_InputTask
    S3_InputTask --> SYS3_SaveGoalEvent
    SYS3_SaveGoalEvent --> SYS3_SyncCal
    SYS3_SyncCal --> S3_OpenCal
    S3_OpenCal --> S3_TickDone
    S3_TickDone --> SYS3_UpdateCompletion
    SYS3_UpdateCompletion --> S3_BackToGoal
    S3_BackToGoal --> SYS3_SyncGoalView
    SYS3_SyncGoalView --> S3_End
```

---

## 4. Tóm tắt các điểm "ăn điểm" khi thuyết minh đồ án

1. **Sự kết hợp thông minh (Create / Update)**: Thay vì vẽ 2 sơ đồ trùng lặp, việc gộp thành **BP-01** thể hiện tư duy thiết kế tái sử dụng (Reusability) của hệ thống.
2. **Khóa bảo vệ dữ liệu lịch sử**: Hệ thống ngăn chặn việc sửa các sự kiện đã kết thúc hoặc đã hoàn thành, chỉ cho phép xóa để tránh gian lận lịch sử học tập.
3. **Tính liên kết liên phân hệ (Cross-module Integration - BP-03)**: Chứng minh tính nhất quán của hệ thống StudyHub: Học viên không phải tạo lịch học hai lần, mục tiêu điểm số gắn liền với hành động thực tế trên thời khóa biểu.
