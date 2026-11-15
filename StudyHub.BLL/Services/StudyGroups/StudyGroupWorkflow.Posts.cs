using StudyHub.DAL.Entities.Pdf;
using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Repositories.Common;
using StudyHub.BLL.Services.StudyGroups.Models;

namespace StudyHub.BLL.Services.StudyGroups;

public sealed partial class StudyGroupWorkflow
{
    public async Task<GroupPostData> AddPostAsync(
        long groupId,
        long userId,
        string content,
        string postType,
        bool isPinned,
        CancellationToken cancellationToken = default)
    {
        var membership = await data.Query<GroupMember>(false)
            .Include(item => item.User)
            .Include(item => item.StudyGroup)
            .SingleOrDefaultAsync(item => item.StudyGroupId == groupId && item.UserId == userId, cancellationToken);
        if (membership?.Status != "Active" || membership.StudyGroup.IsArchived)
            throw new StudyGroupWorkflowException("member_required", "Bạn phải là thành viên đang hoạt động của nhóm.");

        var canManage = membership.StudyGroup.OwnerUserId == userId || membership.MemberRole == "Moderator";
        if (postType == "Announcement" && !canManage)
            throw new StudyGroupWorkflowException("forbidden", "Chỉ trưởng nhóm hoặc điều phối viên được đăng thông báo.");

        if (postType == "Announcement" && isPinned)
        {
            await data.Query<GroupPost>(false)
                .Where(item => item.StudyGroupId == groupId && item.PostType == "Announcement" && item.IsPinned)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsPinned, false), cancellationToken);
        }

        var now = DateTime.UtcNow;
        var post = new GroupPost
        {
            StudyGroupId = groupId,
            AuthorUserId = userId,
            Body = content,
            PostType = postType,
            IsPinned = postType == "Announcement" && isPinned,
            IsDeleted = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        await data.AddAsync(post);
        await data.SaveChangesAsync(cancellationToken);
        return new GroupPostData
        {
            Id = post.GroupPostId.ToString(),
            UserId = userId.ToString(),
            AuthorName = membership.User.DisplayName,
            AuthorAvatarUrl = membership.User.AvatarUrl,
            Content = post.Body,
            PostType = post.PostType,
            IsPinned = post.IsPinned,
            SentAt = AsUtc(post.CreatedAtUtc)
        };
    }

}
