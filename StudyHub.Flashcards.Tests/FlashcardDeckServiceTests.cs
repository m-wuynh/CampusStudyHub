using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs.Flashcards;
using StudyHub.BLL.Services.Flashcards.Implementations;
using StudyHub.DAL.Entities;
using static StudyHub.Flashcards.Tests.FlashcardServiceDatabase;

namespace StudyHub.Flashcards.Tests;

public sealed class FlashcardDeckServiceTests : IDisposable
{
    private readonly FlashcardServiceDatabase database = new();
    private FlashcardDeckService Service => new(database.Repository);

    [Fact]
    public async Task GetMyDecks_SortsOwnedActiveDecksAndProjectsSubjectNames()
    {
        var catalog = new Subject { SubjectId = 1, SubjectCode = "ALG", SubjectName = "Catalog subject" };
        var custom = UserSubject(name: "Custom subject");
        var standard = UserSubject(id: 202, name: null);
        standard.Subject = catalog;
        database.Seed(custom, standard, Deck(), Deck(102, subject: 202), Deck(103, subject: null),
            Deck(104, owner: OtherUserId, subject: null), Deck(105, deleted: true));

        var actual = await Service.GetMyDecksAsync(OwnerId);

        Assert.Collection(actual,
            deck =>
            {
                Assert.Equal(103, deck.DeckId);
                Assert.Null(deck.UserSubjectId);
                Assert.Null(deck.SubjectName);
            },
            deck =>
            {
                Assert.Equal(102, deck.DeckId);
                Assert.Equal(202, deck.UserSubjectId);
                Assert.Equal("Catalog subject", deck.SubjectName);
            },
            deck =>
            {
                Assert.Equal(DeckId, deck.DeckId);
                Assert.Equal("Deck 101", deck.Title);
                Assert.Equal("Private", deck.Visibility);
                Assert.Equal("Custom subject", deck.SubjectName);
            });
    }

    [Theory]
    [InlineData("custom", "Custom", true)]
    [InlineData("catalog", "Catalog", true)]
    [InlineData("archived", "Archived", false)]
    [InlineData("unassigned", null, false)]
    public async Task GetDeckDetail_ReportsSubjectNameAndActiveStatus(
        string subjectKind, string? expectedName, bool active)
    {
        var deck = Deck(subject: subjectKind == "unassigned" ? null : SubjectId);
        if (subjectKind != "unassigned")
        {
            var subject = UserSubject(name: expectedName, archived: subjectKind == "archived");
            if (subjectKind == "catalog")
            {
                subject.CustomSubjectName = null;
                subject.Subject = new Subject { SubjectId = 1, SubjectCode = "CAT", SubjectName = "Catalog" };
            }
            database.Seed(subject);
        }
        database.Seed(deck);

        var detail = Assert.IsType<FlashcardDeckDetailDto>(await Service.GetDeckDetailAsync(DeckId, OwnerId));

        Assert.Equal(DeckId, detail.DeckId);
        Assert.Equal(OwnerId, detail.OwneruserId);
        Assert.Equal(expectedName, detail.SubjectName);
        Assert.Equal(active, detail.HasActiveSubject);
    }

    [Theory]
    [InlineData(999, OwnerId)]
    [InlineData(DeckId, OtherUserId)]
    [InlineData(102, OwnerId)]
    public async Task DeckOperations_RejectMissingForeignOrDeletedDecks(long deckId, long userId)
    {
        database.Seed(Deck(subject: null), Deck(102, subject: null, deleted: true));

        Assert.Null(await Service.GetDeckDetailAsync(deckId, userId));
        Assert.False(await Service.UpdateDeckAsync(deckId, userId, new("Updated", "Description")));
        Assert.False(await Service.DeleteDeckAsync(deckId, userId));

        var original = await database.Context.FlashcardDecks.AsNoTracking().SingleAsync(d => d.DeckId == DeckId);
        Assert.Equal("Deck 101", original.Title);
        Assert.False(original.IsDeleted);
    }

    [Fact]
    public async Task GetSubjectOptions_ReturnsOnlyOwnedActiveSubjectsInNameOrder()
    {
        var catalog = UserSubject(id: 202, name: null);
        catalog.Subject = new Subject { SubjectId = 1, SubjectCode = "CAT", SubjectName = "Biology" };
        database.Seed(UserSubject(name: "Zoology"), catalog,
            UserSubject(id: 203, name: "Archived", archived: true),
            UserSubject(id: 204, owner: OtherUserId, name: "Foreign"));

        Assert.Equal(new[]
        {
            new FlashcardSubjectOptionDto(202, "Biology"),
            new FlashcardSubjectOptionDto(SubjectId, "Zoology")
        }, await Service.GetSubjectOptionsAsync(OwnerId));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("  Description  ", "Description")]
    public async Task CreateDeck_TrimsFieldsAndPersistsOwnedPrivateDeck(string? description, string? expected)
    {
        database.Seed(UserSubject());
        var title = new string('t', 200);

        var id = await Service.CreateDeckAsync(OwnerId, new($"  {title}  ", description, SubjectId));

        var deck = await database.Context.FlashcardDecks.AsNoTracking().SingleAsync();
        Assert.True(id > 0);
        Assert.Equal(id, deck.DeckId);
        Assert.Equal(OwnerId, deck.OwnerUserId);
        Assert.Equal(SubjectId, deck.UserSubjectId);
        Assert.Equal(title, deck.Title);
        Assert.Equal(expected, deck.Description);
        Assert.Equal("Private", deck.Visibility);
        Assert.Null(deck.StudyGroupId);
        Assert.False(deck.IsDeleted);
        Assert.Equal(deck.CreatedAtUtc, deck.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(999L)]
    [InlineData(202L)]
    [InlineData(203L)]
    public async Task CreateDeck_RejectsMissingForeignOrArchivedSubject(long? subjectId)
    {
        database.Seed(UserSubject(id: 202, owner: OtherUserId), UserSubject(id: 203, archived: true));

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => Service.CreateDeckAsync(OwnerId, new("Title", null, subjectId)));

        Assert.Equal("Vui lòng chọn môn học hợp lệ của bạn.", error.Message);
        Assert.Empty(database.Context.FlashcardDecks);
    }

    [Theory]
    [InlineData(null, 0, nameof(CreateFlashcardDeckRequestDto.Title),
        "Tên deck không được để trống.", "Tên bộ flashcard không được để trống")]
    [InlineData("   ", 0, nameof(CreateFlashcardDeckRequestDto.Title),
        "Tên deck không được để trống.", "Tên bộ flashcard không được để trống")]
    [InlineData("long", 0, nameof(CreateFlashcardDeckRequestDto.Title),
        "Tên deck không được vượt quá 200 ký tự.", "Tên bộ flashcard không được vượt quá 200 ký tự")]
    [InlineData("Valid", 2001, nameof(CreateFlashcardDeckRequestDto.Description),
        "Mô tả deck không được vượt quá 2000 ký tự.", "Mô tả không được vượt quá 2000 ký tự")]
    public async Task InvalidDeckFields_ThrowWithExpectedMessageAndParameter(
        string? title, int descriptionLength, string parameter, string createMessage, string updateMessage)
    {
        database.Seed(Deck(subject: null));
        if (title == "long")
            title = new string('t', 201);
        var description = new string('d', descriptionLength);

        var createError = await Assert.ThrowsAsync<ArgumentException>(parameter,
            () => Service.CreateDeckAsync(OwnerId, new(title, description, SubjectId)));
        var updateError = await Assert.ThrowsAsync<ArgumentException>(parameter,
            () => Service.UpdateDeckAsync(DeckId, OwnerId, new(title, description)));

        Assert.Equal(new ArgumentException(createMessage, parameter).Message, createError.Message);
        Assert.Equal(new ArgumentException(updateMessage, parameter).Message, updateError.Message);
        Assert.Equal("Deck 101", (await database.Context.FlashcardDecks.AsNoTracking().SingleAsync()).Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("max length")]
    public async Task UpdateDeck_TrimsDescriptionAndAcceptsExactly2000Characters(string? input)
    {
        database.Seed(UserSubject(), Deck());
        var description = input == "max length" ? new string('d', 2000) : null;
        var submitted = description is null ? input : $"  {description}  ";

        Assert.True(await Service.UpdateDeckAsync(DeckId, OwnerId, new(" Updated ", submitted)));

        var deck = await database.Context.FlashcardDecks.AsNoTracking().SingleAsync();
        Assert.Equal("Updated", deck.Title);
        Assert.Equal(description, deck.Description);
        Assert.Equal(SubjectId, deck.UserSubjectId);
        Assert.True(deck.UpdatedAtUtc > SeedTime);
    }

    [Fact]
    public async Task DeleteDeck_SoftDeletesDeckAndHidesItFromReads()
    {
        database.Seed(Deck(subject: null), Card(11));

        Assert.True(await Service.DeleteDeckAsync(DeckId, OwnerId));

        var deck = await database.Context.FlashcardDecks.AsNoTracking().SingleAsync();
        Assert.True(deck.IsDeleted);
        Assert.True(deck.UpdatedAtUtc > SeedTime);
        Assert.False((await database.Context.Flashcards.AsNoTracking().SingleAsync()).IsDeleted);
        Assert.Empty(await Service.GetMyDecksAsync(OwnerId));
        Assert.Null(await Service.GetDeckDetailAsync(DeckId, OwnerId));
    }

    public void Dispose() => database.Dispose();
}
