using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CampusStudyHub.Data;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace CampusStudyHub.Services;

// Authorization lives here as well as in the UI: never trust a submitted item ID.
public class HubService(IDbContextFactory<HubDb> factory, AuthenticationStateProvider auth)
{
    public async Task<Student> Me(HubDb db)
    {
        var id = (await auth.GetAuthenticationStateAsync()).User.FindFirstValue(ClaimTypes.NameIdentifier);
        var me = id is null ? null : await db.Students.FindAsync(id);
        if (me is null || me.Suspended) throw new UnauthorizedAccessException("Vui lòng đăng nhập; tài khoản phải đang hoạt động.");
        return me;
    }
    public async Task<HubSnapshot> Load()
    {
        await using var db = await factory.CreateDbContextAsync();
        var me = await Me(db);
        var subjects = await db.Memberships.Where(x => x.StudentId == me.Id && !x.Subject.Archived).Select(x => x.Subject).ToListAsync();
        var ids = subjects.Select(x => x.Id).ToArray();
        var items = await db.Items.Include(x => x.Subject).Where(x => ids.Contains(x.SubjectId) && !x.Hidden && (x.Kind == "document" || x.Kind == "group" || x.OwnerId == me.Id)).ToListAsync();
        return new(me, subjects, items,
            await db.Bookmarks.Where(x => x.StudentId == me.Id).Select(x => x.ItemId).ToListAsync(),
            await db.GroupRequests.Where(x => ids.Contains(x.Item.SubjectId)).ToListAsync(),
            await db.Events.Where(x => x.StudentId == me.Id).OrderByDescending(x => x.At).Take(100).ToListAsync(),
            me.IsAdmin ? await db.Reports.Include(x => x.Item).OrderByDescending(x => x.Id).Take(100).ToListAsync() : [],
            me.IsAdmin ? await db.Students.ToListAsync() : []);
    }
    public async Task Profile(Student input)
    {
        Validate(input);
        await using var db = await factory.CreateDbContextAsync(); var me = await Me(db);
        me.University = input.University.Trim(); me.Faculty = input.Faculty.Trim(); me.Cohort = input.Cohort.Trim();
        await db.SaveChangesAsync();
    }
    public async Task SaveSubject(Subject input)
    {
        Validate(input);
        await using var db = await factory.CreateDbContextAsync(); var me = await Me(db);
        if (input.Id == 0)
        {
            var subject = new Subject { Name = input.Name.Trim(), Code = input.Code.Trim(), Lecturer = input.Lecturer, ClassName = input.ClassName, Color = input.Color, OwnerId = me.Id };
            db.Subjects.Add(subject); db.Memberships.Add(new() { Subject = subject, StudentId = me.Id });
            Log(db, me.Id, "subject_created");
        }
        else
        {
            var subject = await db.Subjects.SingleAsync(x => x.Id == input.Id && x.OwnerId == me.Id);
            subject.Name = input.Name; subject.Code = input.Code; subject.Lecturer = input.Lecturer; subject.ClassName = input.ClassName; subject.Color = input.Color;
        }
        await db.SaveChangesAsync();
    }
    public async Task JoinSubject(string code)
    {
        await using var db = await factory.CreateDbContextAsync(); var me = await Me(db);
        var subject = await db.Subjects.SingleOrDefaultAsync(x => x.InviteCode == code.Trim().ToUpperInvariant() && !x.Archived) ?? throw new ValidationException("Mã lớp không tồn tại.");
        if (!await db.Memberships.AnyAsync(x => x.SubjectId == subject.Id && x.StudentId == me.Id))
            db.Memberships.Add(new() { SubjectId = subject.Id, StudentId = me.Id });
        await db.SaveChangesAsync();
    }
    public async Task SaveItem(StudyItem input, string? storedFile = null, string? fileName = null)
    {
        Validate(input);
        if (!new[] { "deadline", "schedule", "document", "group" }.Contains(input.Kind)) throw new ValidationException("Loại dữ liệu không hợp lệ.");
        if (!string.IsNullOrWhiteSpace(input.Url) && !SafeUrl(input.Url)) throw new ValidationException("Liên kết phải bắt đầu bằng https:// hoặc http://.");
        await using var db = await factory.CreateDbContextAsync(); var me = await Me(db);
        await Member(db, me.Id, input.SubjectId);
        var item = input.Id == 0 ? new StudyItem { OwnerId = me.Id, Kind = input.Kind } : await db.Items.SingleAsync(x => x.Id == input.Id && x.OwnerId == me.Id && !x.Hidden);
        if (item.Kind != input.Kind) throw new ValidationException("Không thể đổi loại dữ liệu.");
        if (input.Kind == "document" && string.IsNullOrWhiteSpace(input.Url) && storedFile is null && item.FilePath == "") throw new ValidationException("Hãy chọn file hoặc nhập liên kết tài liệu.");
        item.SubjectId = input.SubjectId; item.Title = input.Title.Trim(); item.Notes = input.Notes; item.When = input.When; item.Hours = input.Hours; item.Url = input.Url.Trim(); item.Capacity = input.Capacity; item.DocumentType = input.DocumentType;
        if (storedFile is not null) { item.FilePath = storedFile; item.FileName = fileName ?? "document.pdf"; }
        if (input.Id == 0) { db.Items.Add(item); Log(db, me.Id, input.Kind == "group" ? "study_group_created" : input.Kind + "_created"); }
        await db.SaveChangesAsync();
    }
    public async Task Act(int id, string action, string reason = "")
    {
        await using var db = await factory.CreateDbContextAsync(); var me = await Me(db);
        var item = await db.Items.SingleAsync(x => x.Id == id && !x.Hidden);
        await Member(db, me.Id, item.SubjectId);
        if (item.Kind is "deadline" or "schedule" && item.OwnerId != me.Id) throw new UnauthorizedAccessException();
        switch (action)
        {
            case "done":
                if (item.OwnerId != me.Id || item.Kind != "deadline") throw new UnauthorizedAccessException();
                item.Done = !item.Done; item.CompletedAt = item.Done ? DateTime.Now : null;
                Log(db, me.Id, item.Done ? "deadline_completed" : "deadline_reopened"); break;
            case "delete":
                if (item.OwnerId != me.Id) throw new UnauthorizedAccessException();
                item.Hidden = true; Log(db, me.Id, "item_archived"); break;
            case "bookmark":
                if (item.Kind is not ("document" or "group")) throw new ValidationException("Không thể lưu mục này.");
                var saved = await db.Bookmarks.FindAsync(id, me.Id);
                if (saved is null) db.Bookmarks.Add(new() { ItemId = id, StudentId = me.Id }); else db.Bookmarks.Remove(saved); break;
            case "report":
                if (item.Kind is not ("document" or "group") || string.IsNullOrWhiteSpace(reason) || reason.Length > 1000) throw new ValidationException("Nhập lý do báo cáo (tối đa 1000 ký tự).");
                db.Reports.Add(new() { ItemId = id, StudentId = me.Id, Reason = reason.Trim() }); break;
            case "join":
                if (item.Kind != "group" || item.OwnerId == me.Id) throw new ValidationException("Bạn là chủ nhóm.");
                if (!await db.GroupRequests.AnyAsync(x => x.ItemId == id && x.StudentId == me.Id)) db.GroupRequests.Add(new() { ItemId = id, StudentId = me.Id });
                Log(db, me.Id, "study_group_requested"); break;
            default: throw new ValidationException("Thao tác không hợp lệ.");
        }
        await db.SaveChangesAsync();
    }
    public async Task ReviewRequest(int id, string studentId, bool accept)
    {
        await using var db = await factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var me = await Me(db);
        var group = await db.Items.SingleAsync(x => x.Id == id && x.OwnerId == me.Id && x.Kind == "group" && !x.Hidden);
        await Member(db, me.Id, group.SubjectId);
        var request = await db.GroupRequests.FindAsync(id, studentId) ?? throw new ValidationException("Không tìm thấy yêu cầu.");
        if (request.Status != "pending") throw new ValidationException("Yêu cầu đã được xử lý.");
        if (accept && await db.GroupRequests.CountAsync(x => x.ItemId == id && x.Status == "accepted") >= group.Capacity - 1) throw new ValidationException("Nhóm đã đủ thành viên.");
        request.Status = accept ? "accepted" : "rejected";
        if (accept) Log(db, studentId, "study_group_joined");
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }
    public async Task Moderate(int reportId, string reason, bool hide)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000) throw new ValidationException("Cần nhập lý do xử lý (tối đa 1000 ký tự).");
        await using var db = await factory.CreateDbContextAsync(); var me = await Me(db);
        if (!me.IsAdmin) throw new UnauthorizedAccessException();
        var report = await db.Reports.Include(x => x.Item).SingleAsync(x => x.Id == reportId);
        report.Item.Hidden = hide; report.Resolution = reason.Trim(); report.ResolvedBy = me.Id;
        Log(db, me.Id, $"moderation:{reportId}:{(hide ? "hidden" : "restored")}");
        await db.SaveChangesAsync();
    }
    public async Task Suspend(string id)
    {
        await using var db = await factory.CreateDbContextAsync(); var me = await Me(db);
        if (!me.IsAdmin || id == me.Id) throw new UnauthorizedAccessException("Không thể khóa chính mình.");
        var user = await db.Students.FindAsync(id) ?? throw new ValidationException("Không tìm thấy tài khoản.");
        user.Suspended = !user.Suspended; Log(db, me.Id, $"account_status:{id}:{user.Suspended}"); await db.SaveChangesAsync();
    }
    private static async Task Member(HubDb db, string id, int subjectId)
    {
        if (!await db.Memberships.AnyAsync(x => x.StudentId == id && x.SubjectId == subjectId && !x.Subject.Archived)) throw new UnauthorizedAccessException("Bạn chưa tham gia môn học này.");
    }
    public static bool SafeUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
    public static void Log(HubDb db, string id, string name) => db.Events.Add(new() { StudentId = id, Name = name });
    private static void Validate(object obj) => Validator.ValidateObject(obj, new ValidationContext(obj), true);
}
public record HubSnapshot(Student Me, List<Subject> Subjects, List<StudyItem> Items, List<int> Bookmarks, List<GroupRequest> Requests, List<ActivityEvent> Events, List<Report> Reports, List<Student> Users);
