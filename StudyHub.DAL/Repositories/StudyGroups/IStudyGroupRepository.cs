using StudyHub.DAL.Repositories.StudyGroups.Models;

namespace StudyHub.DAL.Repositories.StudyGroups;

public interface IStudyGroupRepository
{
    IReadOnlyList<StudyGroupData> GetAll(string? searchText = null);
    StudyGroupData? GetById(string groupId);
    StudyGroupData Add(StudyGroupData group);
    void Update(StudyGroupData group);
}
