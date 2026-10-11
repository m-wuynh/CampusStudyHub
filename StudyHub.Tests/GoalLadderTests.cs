using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs;
using StudyHub.BLL.Services.Grades;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Persistence;
using StudyHub.DAL.Repositories.Common;
using Xunit;

namespace StudyHub.Tests;

public class TestStudyHubDbContext(DbContextOptions<StudyHubDbContext> options) : StudyHubDbContext(options)
{
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        FillRowVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        FillRowVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void FillRowVersions()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            var prop = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "RowVersion");
            if (prop != null && (prop.CurrentValue == null || ((byte[])prop.CurrentValue).Length == 0))
            {
                prop.CurrentValue = new byte[] { 1, 0, 0, 0 };
            }
        }
    }
}

public class GoalLadderTests
{
    private (TestStudyHubDbContext Context, IRepository Repo, GoalService GoalService, GradeService GradeService) CreateServices()
    {
        var options = new DbContextOptionsBuilder<StudyHubDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new TestStudyHubDbContext(options);
        var repo = new EfRepository(context, null!, null!);
        var goalService = new GoalService(repo);
        var gradeService = new GradeService(repo);

        return (context, repo, goalService, gradeService);
    }

    [Fact]
    public async Task CreateGoal_Sequential_6_8_9_MergedIntoSingleGoalLadder_WithCorrectBaseAndAdditionalRungs()
    {
        var (context, repo, goalService, gradeService) = CreateServices();
        long userId = 101;

        // Setup user, gradebook and subject
        var user = new User
        {
            UserId = userId,
            DisplayName = "Test User",
            Email = "test@domain.com",
            EducationLevel = "HighSchool",
            RoleCode = "Student",
            Status = "Active"
        };
        var gradeBook = new GradeBook { GradeBookId = 1, UserId = userId, Name = "Năm học 2024-2025", FromYear = 2024, ToYear = 2025 };
        var userSubject = new UserSubject
        {
            UserSubjectId = 10,
            UserId = userId,
            GradeBookId = 1,
            CustomSubjectName = "Toán",
            CreditWeight = 3,
            ColorHex = "#5138EE",
            IsArchived = false
        };

        context.Users.Add(user);
        context.GradeBooks.Add(gradeBook);
        context.UserSubjects.Add(userSubject);
        await context.SaveChangesAsync();

        // 1. Create first goal: 6.0 -> Must automatically be Base Goal
        await goalService.CreateGoalAsync(userId, new CreateGoalDto(10, "Mục tiêu Toán kỳ này", 6.0m, null, false));

        var goals = await repo.Query<Goal>(false).Where(g => g.UserId == userId).ToListAsync();
        Assert.Single(goals);
        Assert.True(goals[0].IsBaseGoal);
        Assert.Equal(6.0m, goals[0].TargetValue);

        // 2. Create second goal: 8.0 -> Must be additional rung (IsBaseGoal = false)
        await goalService.CreateGoalAsync(userId, new CreateGoalDto(10, "Phấn đấu 8.0 Toán", 8.0m, null, false));

        goals = await repo.Query<Goal>(false).Where(g => g.UserId == userId).OrderBy(g => g.CreatedAtUtc).ToListAsync();
        Assert.Equal(2, goals.Count);
        Assert.True(goals[0].IsBaseGoal);
        Assert.Equal(6.0m, goals[0].TargetValue);
        Assert.False(goals[1].IsBaseGoal);
        Assert.Equal(8.0m, goals[1].TargetValue);

        // 3. Create third goal: 9.0 -> Must be additional rung (IsBaseGoal = false)
        await goalService.CreateGoalAsync(userId, new CreateGoalDto(10, "Chinh phục 9.0 Toán", 9.0m, null, false));

        goals = await repo.Query<Goal>(false).Where(g => g.UserId == userId).OrderBy(g => g.CreatedAtUtc).ToListAsync();
        Assert.Equal(3, goals.Count);
        Assert.True(goals[0].IsBaseGoal);
        Assert.False(goals[1].IsBaseGoal);
        Assert.False(goals[2].IsBaseGoal);
        Assert.Equal(9.0m, goals[2].TargetValue);

        // 4. Validate additional rung constraints
        // Cannot add rung <= base goal (e.g. 5.0)
        var exLower = await Assert.ThrowsAsync<ArgumentException>(() => goalService.CreateGoalAsync(userId, new CreateGoalDto(10, "Thấp hơn gốc", 5.0m, null, false)));
        Assert.Contains("lớn hơn mục tiêu gốc", exLower.Message);

        // Cannot add rung > 10
        var exExceed = await Assert.ThrowsAsync<ArgumentException>(() => goalService.CreateGoalAsync(userId, new CreateGoalDto(10, "Vượt quá 10", 10.5m, null, false)));
        Assert.Contains("(0, 10]", exExceed.Message);

        // Cannot add duplicate rung (e.g. 8.0)
        var exDup = await Assert.ThrowsAsync<ArgumentException>(() => goalService.CreateGoalAsync(userId, new CreateGoalDto(10, "Trùng 8.0", 8.0m, null, false)));
        Assert.Contains("đã có mục tiêu", exDup.Message);

        // 5. Query page and verify exactly ONE Goal Ladder for Math with 3 rungs and AcademicYearName badge
        var page = await gradeService.GetGradePageAsync(userId, 1);
        Assert.Single(page.GoalLadders);

        var mathLadder = page.GoalLadders[0];
        Assert.Equal("Toán", mathLadder.SubjectName);
        Assert.Equal("Năm học 2024–2025", mathLadder.AcademicYearName);
        Assert.True(mathLadder.HasBaseGoal);
        Assert.NotNull(mathLadder.BaseGoal);
        Assert.Equal(6.0m, mathLadder.BaseGoal.TargetValue);
        Assert.True(mathLadder.BaseGoal.IsBaseGoal);

        Assert.NotNull(mathLadder.AdditionalGoals);
        Assert.Equal(2, mathLadder.AdditionalGoals.Count);
        Assert.Equal(8.0m, mathLadder.AdditionalGoals[0].TargetValue);
        Assert.False(mathLadder.AdditionalGoals[0].IsBaseGoal);
        Assert.Equal(9.0m, mathLadder.AdditionalGoals[1].TargetValue);
        Assert.False(mathLadder.AdditionalGoals[1].IsBaseGoal);

        Assert.Equal(3, mathLadder.Goals.Count);
    }

    [Fact]
    public async Task Goals_FromDifferentGradeBooks_AreNotMixed()
    {
        var (context, repo, goalService, gradeService) = CreateServices();
        long userId = 202;

        var user = new User
        {
            UserId = userId,
            DisplayName = "User 2",
            Email = "u2@domain.com",
            EducationLevel = "HighSchool",
            RoleCode = "Student",
            Status = "Active"
        };
        var gbYear1 = new GradeBook { GradeBookId = 10, UserId = userId, Name = "Năm học 2023-2024", FromYear = 2023, ToYear = 2024 };
        var gbYear2 = new GradeBook { GradeBookId = 20, UserId = userId, Name = "Năm học 2024-2025", FromYear = 2024, ToYear = 2025 };

        var mathYear1 = new UserSubject
        {
            UserSubjectId = 101,
            UserId = userId,
            GradeBookId = 10,
            CustomSubjectName = "Toán",
            CreditWeight = 3,
            ColorHex = "#5138EE",
            IsArchived = false
        };

        var mathYear2 = new UserSubject
        {
            UserSubjectId = 201,
            UserId = userId,
            GradeBookId = 20,
            CustomSubjectName = "Toán",
            CreditWeight = 3,
            ColorHex = "#5138EE",
            IsArchived = false
        };

        context.Users.Add(user);
        context.GradeBooks.AddRange(gbYear1, gbYear2);
        context.UserSubjects.AddRange(mathYear1, mathYear2);
        await context.SaveChangesAsync();

        // Year 1: Base goal 5.0
        await goalService.CreateGoalAsync(userId, new CreateGoalDto(101, "Toán năm 1", 5.0m, null, false));

        // Year 2: Base goal 6.0 and additional 8.0
        await goalService.CreateGoalAsync(userId, new CreateGoalDto(201, "Toán năm 2", 6.0m, null, false));
        await goalService.CreateGoalAsync(userId, new CreateGoalDto(201, "Toán năm 2 nâng cao", 8.0m, null, false));

        // Query Year 1 page
        var page1 = await gradeService.GetGradePageAsync(userId, 10);
        Assert.Single(page1.GoalLadders);
        Assert.Equal("Năm học 2023–2024", page1.GoalLadders[0].AcademicYearName);
        Assert.Equal(5.0m, page1.GoalLadders[0].BaseGoal!.TargetValue);
        Assert.Empty(page1.GoalLadders[0].AdditionalGoals ?? new List<GoalDto>());

        // Query Year 2 page
        var page2 = await gradeService.GetGradePageAsync(userId, 20);
        Assert.Single(page2.GoalLadders);
        Assert.Equal("Năm học 2024–2025", page2.GoalLadders[0].AcademicYearName);
        Assert.Equal(6.0m, page2.GoalLadders[0].BaseGoal!.TargetValue);
        var year2AddGoals = page2.GoalLadders[0].AdditionalGoals;
        Assert.NotNull(year2AddGoals);
        Assert.Single(year2AddGoals);
        Assert.Equal(8.0m, year2AddGoals[0].TargetValue);
    }

    [Fact]
    public async Task SiblingUserSubjects_InSameGradeBook_MergeIntoSingleGoalLadder()
    {
        var (context, repo, goalService, gradeService) = CreateServices();
        long userId = 303;

        var user = new User
        {
            UserId = userId,
            DisplayName = "User 3",
            Email = "u3@domain.com",
            EducationLevel = "HighSchool",
            RoleCode = "Student",
            Status = "Active"
        };
        var gb = new GradeBook { GradeBookId = 30, UserId = userId, Name = "Năm học 2024-2025" };

        // 2 separate UserSubject entries for "Toán" in the same GradeBook (e.g. from duplicates)
        var math1 = new UserSubject
        {
            UserSubjectId = 301,
            UserId = userId,
            GradeBookId = 30,
            CustomSubjectName = "Toán",
            CreditWeight = 3,
            ColorHex = "#5138EE",
            IsArchived = false
        };
        var math2 = new UserSubject
        {
            UserSubjectId = 302,
            UserId = userId,
            GradeBookId = 30,
            CustomSubjectName = "Toán",
            CreditWeight = 3,
            ColorHex = "#5138EE",
            IsArchived = false
        };

        context.Users.Add(user);
        context.GradeBooks.Add(gb);
        context.UserSubjects.AddRange(math1, math2);
        await context.SaveChangesAsync();

        // Goal on math1
        await goalService.CreateGoalAsync(userId, new CreateGoalDto(301, "Mục tiêu gốc Toán", 6.0m, null, false));

        // Goal on math2 for the same subject name -> correctly recognizes math1's 6.0 as base goal and adds as rung
        await goalService.CreateGoalAsync(userId, new CreateGoalDto(302, "Mục tiêu 8.0 Toán", 8.0m, null, false));

        var page = await gradeService.GetGradePageAsync(userId, 30);
        // Must be merged into exactly 1 Goal Ladder
        Assert.Single(page.GoalLadders);
        Assert.Equal("Toán", page.GoalLadders[0].SubjectName);
        Assert.Equal(2, page.GoalLadders[0].Goals.Count);
        Assert.Equal(6.0m, page.GoalLadders[0].BaseGoal!.TargetValue);
        Assert.NotNull(page.GoalLadders[0].AdditionalGoals);
        Assert.Equal(8.0m, page.GoalLadders[0].AdditionalGoals[0].TargetValue);
    }
}
