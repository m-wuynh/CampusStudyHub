using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using StudyHub.BLL.DTOs.Flashcards;
using StudyHub.BLL.Services.Flashcards.Implementations;
using StudyHub.BLL.Services.Flashcards.Interfaces;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Enums.Flashcards;
using StudyHub.DAL.Repositories.Common;
using static StudyHub.Flashcards.Tests.FlashcardServiceDatabase;

namespace StudyHub.Flashcards.Tests;

public sealed partial class DbLearnSessionServiceTests
{
    private const long FirstCard = 11;
    private const long SecondCard = 12;
    private static readonly Guid SessionId = Guid.Parse("1afbdd1b-8484-4b77-936f-89f369e13c5d");
    private static readonly Guid Token = Guid.Parse("39b5c666-1dab-492a-9ccd-42ae1059b15d");
    private const string SavedOptions = """[{"Text":"Alpha"},{"Text":"Beta"}]""";
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly Mock<IRepository> repository = new(MockBehavior.Strict);
    private readonly Mock<IFlashcardService> flashcards = new(MockBehavior.Strict);
    private readonly Mock<IDbContextTransaction> transaction = new(MockBehavior.Strict);
    private readonly List<FlashcardLearnSession> sessions = [];
    private readonly List<FlashcardReview> reviews = [];
    private readonly List<Flashcard> storedCards = [];
    private IReadOnlyList<FlashcardDto> cards = [new(FirstCard, "Alpha", "First", 1), new(SecondCard, "Beta", "Second", 2)];
    private FlashcardProgress? progress;
    private DbLearnSessionService Service => new(repository.Object, flashcards.Object);

    public DbLearnSessionServiceTests()
    {
        repository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<FlashcardDeck, bool>>>(), Ct))
            .ReturnsAsync((Expression<Func<FlashcardDeck, bool>> filter, CancellationToken _) =>
                new[] { Deck(), Deck(99, deleted: true) }.AsQueryable().FirstOrDefault(filter));
        repository.Setup(r => r.Query<FlashcardLearnSession>(true)).Returns(() => sessions.AsQueryable());
        repository.Setup(r => r.FindAsync<FlashcardLearnSession>(It.IsAny<object[]>(), Ct))
            .Returns((object[] keys, CancellationToken _) =>
                ValueTask.FromResult(sessions.SingleOrDefault(s => s.SessionId == (Guid)keys[0])));
        repository.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<Flashcard, bool>>>(), Ct))
            .ReturnsAsync((Expression<Func<Flashcard, bool>> filter, CancellationToken _) =>
                storedCards.AsQueryable().Any(filter));
        repository.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<FlashcardReview, bool>>>(), Ct))
            .ReturnsAsync((Expression<Func<FlashcardReview, bool>> filter, CancellationToken _) =>
                reviews.AsQueryable().Any(filter));
        repository.Setup(r => r.ListAsync(It.IsAny<Expression<Func<FlashcardReview, bool>>>(), Ct))
            .ReturnsAsync((Expression<Func<FlashcardReview, bool>> filter, CancellationToken _) =>
                reviews.AsQueryable().Where(filter).ToList());
        repository.Setup(r => r.AddAsync(It.IsAny<FlashcardLearnSession>(), Ct))
            .Callback((FlashcardLearnSession session, CancellationToken _) => sessions.Add(session))
            .Returns(Task.CompletedTask);
        repository.Setup(r => r.Update(It.IsAny<FlashcardLearnSession>()));
        repository.Setup(r => r.SaveChangesAsync(Ct)).ReturnsAsync(1);
        flashcards.Setup(s => s.GetStudyCardsAsync(DeckId, OwnerId, Ct)).ReturnsAsync(() => cards);
        repository.Setup(r => r.BeginTransactionAsync(Ct)).ReturnsAsync(transaction.Object);
        transaction.Setup(t => t.CommitAsync(Ct)).Returns(Task.CompletedTask);
        transaction.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);
        repository.Setup(r => r.FindAsync<FlashcardProgress>(It.IsAny<object[]>(), Ct))
            .Returns((object[] keys, CancellationToken _) =>
                ValueTask.FromResult(progress?.UserId == (long)keys[0] && progress.FlashcardId == (long)keys[1]
                    ? progress : null));
        repository.Setup(r => r.AddAsync(It.IsAny<FlashcardProgress>(), Ct))
            .Callback((FlashcardProgress entity, CancellationToken _) => progress = entity)
            .Returns(Task.CompletedTask);
        repository.Setup(r => r.AddAsync(It.IsAny<FlashcardReview>(), Ct))
            .Callback((FlashcardReview review, CancellationToken _) => reviews.Add(review))
            .Returns(Task.CompletedTask);
    }

    [Theory]
    [InlineData(0, OwnerId)]
    [InlineData(DeckId, 0)]
    [InlineData(DeckId, OtherUserId)]
    [InlineData(99, OwnerId)]
    public async Task Start_ReturnsNullForInvalidIdsOrInaccessibleDeck(long deckId, long userId)
    {
        Assert.Null(await Service.StartOrResumeAsync(deckId, userId));
        repository.Verify(r => r.AddAsync(It.IsAny<FlashcardLearnSession>(), Ct), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Start_CreatesNewSessionAndIgnoresClosedOrForeignSessions(bool shuffle)
    {
        AddSession().CompletedAtUtc = SeedTime;
        AddSession(Guid.NewGuid()).InvalidatedAtUtc = SeedTime;
        AddSession(Guid.NewGuid()).UserId = OtherUserId;
        AddSession(Guid.NewGuid()).DeckId = 99;

        var id = await Service.StartOrResumeAsync(DeckId, OwnerId, Ct, shuffle);

        var session = Assert.Single(sessions, s => s.SessionId == id);
        Assert.NotEqual(Guid.Empty, session.SessionId);
        Assert.Equal(5, sessions.Count);
        Assert.Equal(OwnerId, session.UserId);
        Assert.Equal(DeckId, session.DeckId);
        Assert.Equal(shuffle, session.ShuffleEnabled);
        Assert.Null(session.CompletedAtUtc);
        repository.Verify(r => r.SaveChangesAsync(Ct), Times.Once);
    }

    [Fact]
    public async Task Start_ResumesMostRecentUnchangedSessionAndKeepsShuffleMode()
    {
        var session = AddSession();
        session.ShuffleEnabled = true;
        AddSession(Guid.NewGuid()).StartedAtUtc = SeedTime.AddDays(-1);
        storedCards.Add(Card(40, deck: 99));
        storedCards[0].CreatedAtUtc = SeedTime.AddDays(1);
        storedCards.Add(Card(FirstCard));

        Assert.Equal(SessionId, await Service.StartOrResumeAsync(DeckId, OwnerId, Ct, false));
        Assert.True(session.ShuffleEnabled);
        Assert.Null(session.InvalidatedAtUtc);
        flashcards.Verify(s => s.GetStudyCardsAsync(DeckId, OwnerId, Ct), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(Ct), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Start_InvalidatesChangedSessionAndReplacesItOnlyWithEligibleDeck(bool eligible)
    {
        var old = AddSession(withQuestion: true);
        var changed = Card(FirstCard);
        changed.UpdatedAtUtc = SeedTime.AddSeconds(1);
        storedCards.Add(changed);
        if (!eligible)
            cards = [new(FirstCard, "Alpha", "Question", 1), new(SecondCard, "Beta", " question ", 2)];

        var id = await Service.StartOrResumeAsync(DeckId, OwnerId);

        Assert.NotNull(old.InvalidatedAtUtc);
        Assert.Null(old.CurrentCardId);
        Assert.Null(old.QuestionToken);
        Assert.Null(old.QuestionOptionsJson);
        repository.Verify(r => r.Update(old), Times.Once);
        if (eligible)
        {
            Assert.NotNull(id);
            Assert.NotEqual(SessionId, id);
            Assert.Equal(id, sessions[1].SessionId);
            repository.Verify(r => r.SaveChangesAsync(Ct), Times.Exactly(2));
        }
        else
        {
            Assert.Null(id);
            Assert.Single(sessions);
            repository.Verify(r => r.SaveChangesAsync(Ct), Times.Once);
        }
    }

    [Fact]
    public async Task Start_RequiresAtLeastTwoDistinctAnswers()
    {
        cards = [new(FirstCard, "Alpha", "First", 1), new(SecondCard, " alpha ", "Second", 2)];

        Assert.Null(await Service.StartOrResumeAsync(DeckId, OwnerId));
        Assert.Empty(sessions);
        repository.Verify(r => r.SaveChangesAsync(Ct), Times.Never);
    }

    [Theory]
    [InlineData(2601)]
    [InlineData(2627)]
    public async Task Start_ReturnsWinningSessionAfterDuplicateActiveSessionInsert(int sqlNumber)
    {
        repository.Setup(r => r.SaveChangesAsync(Ct)).Returns(() =>
        {
            sessions.Clear();
            AddSession(Guid.NewGuid()).UserId = OtherUserId;
            AddSession(Guid.NewGuid()).DeckId = 99;
            AddSession(Guid.NewGuid()).CompletedAtUtc = SeedTime;
            AddSession(Guid.NewGuid()).InvalidatedAtUtc = SeedTime;
            AddSession();
            throw new DbUpdateException("Insert failed", SqlExceptionFactory.Create(sqlNumber, "UX_LearnSession_Active"));
        });

        Assert.Equal(SessionId, await Service.StartOrResumeAsync(DeckId, OwnerId));
    }

    [Theory]
    [InlineData(2601, "OtherIndex")]
    [InlineData(1205, "UX_LearnSession_Active")]
    [InlineData(null, "")]
    public async Task Start_PropagatesUnrelatedDatabaseErrors(int? sqlNumber, string message)
    {
        var inner = sqlNumber is int number ? SqlExceptionFactory.Create(number, message) : null;
        var failure = new DbUpdateException("Save failed", inner);
        repository.Setup(r => r.SaveChangesAsync(Ct)).ThrowsAsync(failure);

        var actual = await Assert.ThrowsAsync<DbUpdateException>(() => Service.StartOrResumeAsync(DeckId, OwnerId));

        Assert.Same(failure, actual);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("foreign user")]
    [InlineData("wrong deck")]
    public async Task GetStateAndResult_ReturnNullForUnknownOrForeignSession(string scenario)
    {
        if (scenario != "missing")
        {
            var session = AddSession();
            if (scenario == "foreign user")
                session.UserId = OtherUserId;
            else
                session.DeckId = 99;
        }

        Assert.Null(await Service.GetStateAsync(SessionId, DeckId, OwnerId));
        Assert.Null(await Service.GetResultAsync(SessionId, DeckId, OwnerId));
        Assert.Null(await Service.GetOrIssueQuestionAsync(SessionId, DeckId, OwnerId, FirstCard));
        Assert.Null(await Service.SubmitAnswerAsync(SessionId, DeckId, OwnerId, Token, new(FirstCard, "Alpha")));
        transaction.Verify(t => t.CommitAsync(Ct), Times.Never);
    }

    [Theory]
    [InlineData("invalidated", "Phiên học đã bị hủy do Deck thay đổi. Hãy bắt đầu lại.")]
    [InlineData("new card", "Deck đã thay đổi. Hãy bắt đầu phiên mới")]
    [InlineData("legacy without reviews", "Không đủ dữ liệu để khôi phục kết quả phiên học cũ.")]
    [InlineData("too few answers", "Deck không đủ câu hỏi để học")]
    [InlineData("ambiguous deck", "Khong còn Card hợp lệ trong Deck")]
    public async Task GetState_ThrowsExpectedErrorWhenSessionCannotContinue(string cause, string expected)
    {
        var session = AddSession();
        switch (cause)
        {
            case "invalidated":
                session.InvalidatedAtUtc = SeedTime;
                break;
            case "new card":
                var changed = Card(FirstCard);
                changed.CreatedAtUtc = SeedTime.AddSeconds(1);
                storedCards.Add(changed);
                break;
            case "legacy without reviews":
                session.CompletedAtUtc = SeedTime;
                break;
            case "too few answers":
                cards = [];
                break;
            case "ambiguous deck":
                cards = [new(FirstCard, "Alpha", "Question", 1), new(SecondCard, "Beta", " question ", 2)];
                break;
        }

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Service.GetStateAsync(SessionId, DeckId, OwnerId));

        Assert.Equal(expected, error.Message);
    }

    [Theory]
    [InlineData(true, 4)]
    [InlineData(false, 2)]
    public async Task GetResult_UsesCompletedSnapshotOrLegacyDistinctReviewedCards(bool hasSnapshot, int totalCards)
    {
        var session = AddSession();
        session.CompletedAtUtc = SeedTime.AddMinutes(1);
        session.CompletedTotalCards = hasSnapshot ? totalCards : null;
        AddReview(FirstCard, FlashcardReviewRating.Good);
        AddReview(FirstCard, FlashcardReviewRating.Again);
        AddReview(SecondCard, FlashcardReviewRating.Good);
        AddReview(99, FlashcardReviewRating.Good).UserId = OtherUserId;
        AddReview(99, FlashcardReviewRating.Good).SessionId = Guid.NewGuid();
        var newCard = Card(99);
        newCard.UpdatedAtUtc = SeedTime.AddDays(1);
        storedCards.Add(newCard);

        var state = await Service.GetStateAsync(SessionId, DeckId, OwnerId);
        var result = await Service.GetResultAsync(SessionId, DeckId, OwnerId);

        Assert.Equal(new DbLearnStateDto(new(SessionId, DeckId, totalCards, 0, 0, totalCards, true), null), state);
        Assert.Equal(new LearnResultDto(SessionId, DeckId, 3, 2, 1, 66.67, totalCards, totalCards, 0, true), result);
        flashcards.Verify(s => s.GetStudyCardsAsync(DeckId, OwnerId, Ct), Times.Never);
    }

    [Fact]
    public async Task GetState_ExcludesAmbiguousPromptsAndPrioritizesFewerAttempts()
    {
        AddSession();
        cards =
        [
            new(FirstCard, "Alpha", "First", 1),
            new(SecondCard, "Beta", "Second", 2),
            new(13, "Gamma", "Third", 4),
            new(14, "Delta", "Fourth", 3),
            new(15, "Epsilon", " fifth ", 0),
            new(16, "Zeta", "FIFTH", 0)
        ];
        AddReview(FirstCard, FlashcardReviewRating.Again);
        AddReview(SecondCard, FlashcardReviewRating.Good);
        AddReview(SecondCard, FlashcardReviewRating.Good);

        Assert.Equal(new DbLearnStateDto(new(SessionId, DeckId, 4, 2, 1, 1, false), 14),
            await Service.GetStateAsync(SessionId, DeckId, OwnerId));
    }

    [Fact]
    public async Task GetState_ShuffleOrderIsStableWithinSessionAndResultStartsAtZero()
    {
        AddSession().ShuffleEnabled = true;
        cards = [new(FirstCard, "Alpha", "First", 2), new(SecondCard, "Beta", "Second", 1)];

        var first = await Service.GetStateAsync(SessionId, DeckId, OwnerId);
        cards = cards.Reverse().ToArray();
        var second = await Service.GetStateAsync(SessionId, DeckId, OwnerId);

        Assert.NotNull(first);
        Assert.True(first.Session.ShuffleEnabled);
        Assert.Equal(FirstCard, first.NextCardId);
        Assert.Equal(first, second);
        Assert.Equal(new LearnResultDto(SessionId, DeckId, 0, 0, 0, 0, 2, 0, 2, false),
            await Service.GetResultAsync(SessionId, DeckId, OwnerId));
    }

    private FlashcardLearnSession AddSession(Guid? id = null, bool withQuestion = false)
    {
        var session = new FlashcardLearnSession
        {
            SessionId = id ?? SessionId,
            UserId = OwnerId,
            DeckId = DeckId,
            StartedAtUtc = SeedTime,
            CurrentCardId = withQuestion ? FirstCard : null,
            QuestionToken = withQuestion ? Token : null,
            QuestionOptionsJson = withQuestion ? SavedOptions : null
        };
        sessions.Add(session);
        return session;
    }

    private FlashcardReview AddReview(long cardId, FlashcardReviewRating rating)
    {
        var review = new FlashcardReview
        {
            ReviewId = reviews.Count + 1,
            RequestId = Guid.NewGuid(),
            SessionId = SessionId,
            UserId = OwnerId,
            FlashcardId = cardId,
            Rating = (byte)rating,
            ReviewedAtUtc = SeedTime.AddMinutes(reviews.Count + 1)
        };
        reviews.Add(review);
        return review;
    }
}
