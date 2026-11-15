using StudyHub.BLL.DTOs.StudyGroups;

namespace StudyHub.Web.ViewModels.StudyGroups;

public sealed record SharedNotePage(string Title, string Body);
public sealed record GroupResourceViewModel(long GroupId, long ResourceId, SharedContentData Content, IReadOnlyList<SharedNotePage> Pages);
