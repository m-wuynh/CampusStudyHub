using StudyHub.BLL.DTOs;

namespace StudyHub.Web.ViewModels.Grades;

/// <summary>Màn "Bảng điểm" và "Mục tiêu học tập".</summary>
public sealed record GradesIndexViewModel(GradesPageDto Data);

/// <summary>Màn "Tổng quan năm học".</summary>
public sealed record GradesOverviewViewModel(GradesOverviewDto Data);

/// <summary>Màn "Phân tích học tập".</summary>
public sealed record GradesStatisticsViewModel(GradeStatisticsPageDto Data);

/// <summary>Màn "Chi tiết môn" và "Hành trình mục tiêu".</summary>
public sealed record GradesSubjectViewModel(SubjectDetailPageDto Data);
