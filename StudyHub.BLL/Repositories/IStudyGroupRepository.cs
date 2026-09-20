using StudyHub.BLL.Entities;

namespace StudyHub.BLL.Repositories;

public interface IStudyGroupRepository
{
    IReadOnlyList<StudyGroup> GetAll();
    StudyGroup? GetById(string groupId);
    StudyGroup Add(StudyGroup group);
    void Update(StudyGroup group);
}
