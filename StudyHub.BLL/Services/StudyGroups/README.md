# StudyGroups layers

- Web controllers handle HTTP, form validation, feedback, and redirects.
- `StudyGroupService` and `GroupLearningService` validate input, resolve the current user, and map application results/errors.
- `StudyGroupWorkflow` owns group lifecycle, membership transitions, invitations, posting permissions, capacity, and resource cleanup. Partial files separate these use cases.
- `GroupLearningWorkflow` owns learning permissions, session and assignment transitions, submissions, sharing, and weekly progress calculations.
- `Models` contains internal workflow input/output models. Public application DTOs remain in `BLL/DTOs`.
- DAL `IRepository`/`EfRepository` provides entity queries, tracking, persistence, transactions, and database conflict translation. Workflows compose queries through this existing repository abstraction; they do not receive `StudyHubDbContext`.
- PDF sharing is a BLL-to-BLL call through `BLL/Services/Pdf/Sharing/IPdfSharingService`. DAL must not resolve business services.

Keep membership/state checks and their writes inside the same transaction. Moving checks into controllers or validating before opening the transaction can introduce races. Serializable transactions and revision checks are intentionally preserved.

`IRepository.ExecuteAsync` translates SQL deadlocks, duplicate keys, and EF concurrency failures into `DataConflictException`. Application services map that to a user-facing conflict. Business rejections use `StudyGroupWorkflowException` in BLL.
