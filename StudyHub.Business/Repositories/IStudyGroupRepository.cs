using StudyHub.Business.Entities;

namespace StudyHub.Business.Repositories;

public interface IStudyGroupRepository
{
    IReadOnlyList<StudyGroup> GetAll();
    StudyGroup? GetById(string groupId);
    void Add(StudyGroup group);
    void Update(StudyGroup group);
}
