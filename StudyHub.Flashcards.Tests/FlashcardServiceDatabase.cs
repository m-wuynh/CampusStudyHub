using Microsoft.EntityFrameworkCore;
using Moq;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Persistence;
using StudyHub.DAL.Repositories.Auth;
using StudyHub.DAL.Repositories.Common;
using StudyHub.DAL.Repositories.StudyGroups;

namespace StudyHub.Flashcards.Tests;

internal sealed class FlashcardServiceDatabase : IDisposable
{
    internal const long OwnerId = 21;
    internal const long OtherUserId = 22;
    internal const long DeckId = 101;
    internal const long SubjectId = 201;
    internal static readonly DateTime SeedTime = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    internal StudyHubDbContext Context { get; } = new(new DbContextOptionsBuilder<StudyHubDbContext>()
        // SQL Server generates Flashcard.RowVersion on insert. InMemory leaves it null.
        .UseInMemoryDatabase(Guid.NewGuid().ToString(), options => options.EnableNullChecks(false))
        .Options);

    internal IRepository Repository => new EfRepository(
        Context, Mock.Of<IGoogleAuthRepository>(), Mock.Of<IStudyGroupRepository>());

    internal void Seed(params object[] entities)
    {
        Context.AddRange(entities);
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
    }

    internal static FlashcardDeck Deck(
        long id = DeckId, long owner = OwnerId, long? subject = SubjectId, bool deleted = false) => new()
    {
        DeckId = id,
        OwnerUserId = owner,
        UserSubjectId = subject,
        Title = $"Deck {id}",
        Visibility = "Private",
        IsDeleted = deleted,
        CreatedAtUtc = SeedTime,
        UpdatedAtUtc = SeedTime.AddMinutes(id)
    };

    internal static UserSubject UserSubject(
        long id = SubjectId, long owner = OwnerId, string? name = "Algorithms", bool archived = false) => new()
    {
        UserSubjectId = id,
        UserId = owner,
        CustomSubjectName = name,
        IsArchived = archived,
        ColorHex = "#123456"
    };

    internal static Flashcard Card(
        long id,
        string front = "Front",
        string back = "Back",
        int order = 0,
        long deck = DeckId,
        bool deleted = false) => new()
    {
        FlashcardId = id,
        DeckId = deck,
        FrontText = front,
        BackText = back,
        SortOrder = order,
        IsDeleted = deleted,
        CreatedAtUtc = SeedTime,
        UpdatedAtUtc = SeedTime,
        RowVersion = [0]
    };

    public void Dispose() => Context.Dispose();
}
