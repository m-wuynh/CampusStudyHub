using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs.Flashcards;
using StudyHub.BLL.Services.Flashcards.Implementations;
using static StudyHub.Flashcards.Tests.FlashcardServiceDatabase;

namespace StudyHub.Flashcards.Tests;

public sealed class FlashcardServiceTests : IDisposable
{
    private readonly FlashcardServiceDatabase database = new();
    private FlashcardService Service => new(database.Repository);

    [Fact]
    public async Task GetCards_ReturnsOnlyActiveCardsInSortOrderThenId()
    {
        database.Seed(Deck(), Deck(102), Card(13, order: 3), Card(12, "Alpha", "First", order: 1),
            Card(11, "Beta", "Second", order: 1), Card(14, deck: 102), Card(15, deleted: true));

        var expected = new[]
        {
            new FlashcardDto(11, "Beta", "Second", 1),
            new FlashcardDto(12, "Alpha", "First", 1),
            new FlashcardDto(13, "Front", "Back", 3)
        };

        Assert.Equal(expected, await Service.GetCardsByDeckAsync(DeckId, OwnerId));
        Assert.Equal(expected, await Service.GetStudyCardsAsync(DeckId, OwnerId));
        Assert.Empty(await Service.GetStudyCardsAsync(DeckId, OtherUserId));
        Assert.Empty(await Service.GetCardsByDeckAsync(999, OwnerId));
    }

    [Theory]
    [InlineData(999, OwnerId)]
    [InlineData(DeckId, OtherUserId)]
    [InlineData(102, OwnerId)]
    public async Task CreateCard_RejectsMissingForeignOrDeletedDeck(long deckId, long userId)
    {
        database.Seed(Deck(), Deck(102, deleted: true));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Service.CreateCardAsync(deckId, userId, new("Front", "Back", null)));

        Assert.Equal("Deck không tồn tại hoặc không có quyền chỉnh sửa", error.Message);
        Assert.Empty(database.Context.Flashcards);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(999L)]
    [InlineData(202L)]
    [InlineData(203L)]
    public async Task CreateCard_RequiresActiveOwnedSubject(long? subjectId)
    {
        database.Seed(Deck(subject: subjectId), UserSubject(id: 202, owner: OtherUserId),
            UserSubject(id: 203, archived: true));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Service.CreateCardAsync(DeckId, OwnerId, new("Front", "Back", null)));

        Assert.Equal("Bộ thẻ cần gắn với môn học đang hoạt động để thêm thẻ mới.", error.Message);
        Assert.Empty(database.Context.Flashcards);
    }

    [Theory]
    [InlineData(null, false, 0)]
    [InlineData(null, true, 8)]
    [InlineData(0, true, 0)]
    [InlineData(3, true, 3)]
    public async Task CreateCard_TrimsTextAndPersistsExplicitOrNextSortOrder(
        int? sortOrder, bool existingCard, int expectedOrder)
    {
        database.Seed(UserSubject(), Deck(), Deck(102));
        database.Seed(Card(15, order: 99, deleted: true), Card(16, order: 99, deck: 102));
        if (existingCard)
            database.Seed(Card(17, order: 7));
        var front = new string('f', 4000);
        var back = new string('b', 4000);

        var id = await Service.CreateCardAsync(DeckId, OwnerId, new($"  {front}  ", $" {back} ", sortOrder));

        var card = await database.Context.Flashcards.AsNoTracking().SingleAsync(c => c.FlashcardId == id);
        Assert.Equal(DeckId, card.DeckId);
        Assert.Equal(front, card.FrontText);
        Assert.Equal(back, card.BackText);
        Assert.Equal(expectedOrder, card.SortOrder);
        Assert.False(card.IsDeleted);
        Assert.Equal(card.CreatedAtUtc, card.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(null, "Back", 0, "Mặt trước flashcard không được để trống", "Mặt trước flashcard không được để trống")]
    [InlineData("  ", "Back", 0, "Mặt trước flashcard không được để trống", "Mặt trước flashcard không được để trống")]
    [InlineData("Front", null, 0, "Mặt sau flashcard không được để trống", "Mặt sau flashcard không được để trống")]
    [InlineData("Front", "  ", 0, "Mặt sau flashcard không được để trống", "Mặt sau flashcard không được để trống")]
    [InlineData("over limit", "Back", 0, "Mặt trước không được vượt quá 4000 ký tự", "Mặt trước hoặc mặt sau không được vượt quá 4000 ký tự")]
    [InlineData("Front", "over limit", 0, "Mặt sau không được vượt quá 4000 ký tự", "Mặt trước hoặc mặt sau không được vượt quá 4000 ký tự")]
    [InlineData("Front", "Back", -1, "Thứ tự thẻ không được nhỏ hơn 0", "Thứ tự thẻ không được nhỏ hơn 0")]
    public async Task InvalidCardFields_ThrowExpectedErrorsWithoutSaving(
        string? front, string? back, int order, string createMessage, string updateMessage)
    {
        database.Seed(UserSubject(), Deck(), Card(11));
        if (front == "over limit")
            front = new string('f', 4001);
        if (back == "over limit")
            back = new string('b', 4001);

        var createError = await Assert.ThrowsAsync<ArgumentException>(
            () => Service.CreateCardAsync(DeckId, OwnerId, new(front, back, order)));
        var updateError = await Assert.ThrowsAsync<ArgumentException>(
            () => Service.UpdateCardAsync(DeckId, 11, OwnerId, new(front, back, order)));

        Assert.Equal(createMessage, createError.Message);
        Assert.Equal(updateMessage, updateError.Message);
        var stored = await database.Context.Flashcards.AsNoTracking().SingleAsync();
        Assert.Equal("Front", stored.FrontText);
        Assert.Equal("Back", stored.BackText);
        Assert.Equal(SeedTime, stored.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(DeckId, 99, OwnerId)]
    [InlineData(DeckId, 12, OwnerId)]
    [InlineData(102, 11, OwnerId)]
    [InlineData(DeckId, 11, OtherUserId)]
    [InlineData(103, 13, OwnerId)]
    public async Task UpdateAndDeleteCard_RejectUnavailableCardOrDeck(long deckId, long cardId, long userId)
    {
        database.Seed(Deck(), Deck(102), Deck(103, deleted: true), Card(11),
            Card(12, deleted: true), Card(13, deck: 103));

        Assert.False(await Service.UpdateCardAsync(deckId, cardId, userId, new("Changed", "Changed", 3)));
        Assert.False(await Service.DeleteCardAsync(deckId, cardId, userId));

        var original = await database.Context.Flashcards.AsNoTracking().SingleAsync(c => c.FlashcardId == 11);
        Assert.Equal("Front", original.FrontText);
        Assert.False(original.IsDeleted);
    }

    [Fact]
    public async Task UpdateCard_PersistsChangesAndDeleteCard_SoftDeletesIt()
    {
        database.Seed(Deck(), Card(11));
        var front = new string('f', 4000);
        var back = new string('b', 4000);

        Assert.True(await Service.UpdateCardAsync(DeckId, 11, OwnerId, new($"  {front} ", $" {back} ", 5)));
        var listed = Assert.Single(await Service.GetCardsByDeckAsync(DeckId, OwnerId));
        Assert.Equal(new FlashcardDto(11, front, back, 5), listed);
        database.Context.ChangeTracker.Clear();

        Assert.True(await Service.DeleteCardAsync(DeckId, 11, OwnerId));

        var stored = await database.Context.Flashcards.AsNoTracking().SingleAsync();
        Assert.True(stored.IsDeleted);
        Assert.True(stored.UpdatedAtUtc > SeedTime);
        Assert.Empty(await Service.GetCardsByDeckAsync(DeckId, OwnerId));
        Assert.False(await Service.DeleteCardAsync(DeckId, 11, OwnerId));
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("empty")]
    [InlineData("one answer")]
    [InlineData("missing card")]
    [InlineData("ambiguous")]
    public async Task LearnQuestionAndGrading_ReturnNullWhenCardOrDeckCannotBeUsed(string scenario)
    {
        database.Seed(Deck());
        var user = scenario == "foreign" ? OtherUserId : OwnerId;
        var cardId = scenario == "missing card" ? 99 : 11;
        if (scenario != "empty")
        {
            var second = scenario switch
            {
                "one answer" => Card(12, " alpha ", "Other"),
                "ambiguous" => Card(12, "Beta", " question "),
                _ => Card(12, "Beta", "Other")
            };
            database.Seed(Card(11, "Alpha", "Question"), second);
        }

        Assert.Null(await Service.GetLearnQuestionAsync(DeckId, user, cardId));
        Assert.Null(await Service.GradeLearnAnswerAsync(DeckId, user, new(cardId, "Alpha")));
        if (scenario == "ambiguous")
            Assert.Null(await Service.GetLearnQuestionAsync(DeckId, user));
    }

    [Fact]
    public async Task GetLearnQuestion_ChoosesAnEligiblePromptAndUniqueOptions()
    {
        database.Seed(Deck(), Card(11, " Alpha ", "Question"), Card(12, " alpha ", " question "),
            Card(13, "Beta", "Ambiguous"), Card(14, "Gamma", " ambiguous "),
            Card(15, "Delta", " AMBIGUOUS "), Card(16, "Epsilon", "Ambiguous"),
            Card(17, "Deleted", "Question", deleted: true));

        var requested = Assert.IsType<FlashcardLearnQuestionDto>(
            await Service.GetLearnQuestionAsync(DeckId, OwnerId, 11));
        Assert.Equal(11, requested.QuestionCardId);
        Assert.Equal("Question", requested.QuestionText);
        Assert.Equal(4, requested.Options.Count);
        var texts = requested.Options.Select(option => option.Text).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Equal(4, texts.Count);
        Assert.Contains("Alpha", texts);
        Assert.All(texts, text => Assert.Contains(text, new[] { "Alpha", "Beta", "Gamma", "Delta", "Epsilon" }));

        var random = Assert.IsType<FlashcardLearnQuestionDto>(await Service.GetLearnQuestionAsync(DeckId, OwnerId));
        Assert.Contains(random.QuestionCardId, new long[] { 11, 12 });
    }

    [Theory]
    [InlineData("  aLPHa  ", "aLPHa", true)]
    [InlineData(" Beta ", "Beta", false)]
    public async Task GradeLearnAnswer_TrimsAndComparesAnswerIgnoringCase(
        string submitted, string expectedSelected, bool isCorrect)
    {
        database.Seed(Deck(), Card(11, " Alpha ", "Question"), Card(12, "Beta", "Other"));

        Assert.Equal(new FlashcardLearnAnswerResultDto(11, expectedSelected, "Alpha", isCorrect),
            await Service.GradeLearnAnswerAsync(DeckId, OwnerId, new(11, submitted)));
    }

    [Theory]
    [InlineData(null, "Vui lòng chọn một đáp án")]
    [InlineData("  ", "Vui lòng chọn một đáp án")]
    [InlineData("Deleted", "Đáp án được chọn không tồn tại trong bộ thẻ")]
    public async Task GradeLearnAnswer_RejectsBlankAndUnknownAnswers(string? submitted, string message)
    {
        database.Seed(Deck(), Card(11, "Alpha", "Question"), Card(12, "Beta", "Other"),
            Card(13, "Deleted", "Deleted", deleted: true));
        const string parameter = nameof(SubmitFlashcardLearnAnswerRequestDto.SelectedAnswer);

        var error = await Assert.ThrowsAsync<ArgumentException>(parameter,
            () => Service.GradeLearnAnswerAsync(DeckId, OwnerId, new(11, submitted)));

        Assert.Equal(new ArgumentException(message, parameter).Message, error.Message);
    }

    public void Dispose() => database.Dispose();
}
