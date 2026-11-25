# StudyGroups layers

- Web controllers call `IStudyGroupService` and `IGroupLearningService` for business operations.
- `StudyGroupService` handles group lifecycle, membership, invitations, posts, permissions, capacity, and resource cleanup.
- `GroupLearningService` handles sessions, assignments, submissions, questions, sharing, and weekly progress.
- `BLL/DTOs/StudyGroups` contains the study-group data transfer types used by these services. Study-group request and response DTOs are stored in the same folder.
- DAL `IRepository`/`EfRepository` provides queries, tracking, persistence, transactions, and database conflict translation.

Keep membership/state checks and their writes inside the same transaction. Preserve serializable transactions and revision checks where required.

`IRepository.ExecuteAsync` translates database conflicts into `DataConflictException`. Services map these and business rejections to application errors.
