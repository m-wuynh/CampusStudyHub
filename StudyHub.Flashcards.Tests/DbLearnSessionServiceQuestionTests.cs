using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Moq;
using StudyHub.BLL.DTOs.Flashcards;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Enums.Flashcards;
using static StudyHub.Flashcards.Tests.FlashcardServiceDatabase;

namespace StudyHub.Flashcards.Tests;

public sealed partial class DbLearnSessionServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClosedSession_CannotIssueOrAcceptMoreAnswers(bool invalidated)
    {
        var session = AddSession(withQuestion: true);
        if (invalidated)
            session.InvalidatedAtUtc = SeedTime;
        else
            session.CompletedAtUtc = SeedTime;

        Assert.Null(await Service.GetOrIssueQuestionAsync(SessionId, DeckId, OwnerId, FirstCard));
        Assert.Null(await Service.SubmitAnswerAsync(SessionId, DeckId, OwnerId, Token, new(FirstCard, "Alpha")));
        Assert.Empty(reviews);
        repository.Verify(r => r.SaveChangesAsync(Ct), Times.Never);
        transaction.Verify(t => t.CommitAsync(Ct), Times.Never);
    }

    [Theory]
    [InlineData("missing state")]
    [InlineData("all cards learned")]
    [InlineData("cards disappeared")]
    [InlineData("question unavailable")]
    public async Task IssueQuestion_ReturnsNullWhenItCannotIssue(string cause)
    {
        var session = AddSession();
        switch (cause)
        {
            case "missing state":
                repository.SetupSequence(r => r.FindAsync<FlashcardLearnSession>(It.IsAny<object[]>(), Ct))
                    .Returns(ValueTask.FromResult<FlashcardLearnSession?>(session))
                    .Returns(ValueTask.FromResult<FlashcardLearnSession?>(null));
                break;
            case "all cards learned":
                AddReview(FirstCard, FlashcardReviewRating.Good);
                AddReview(FirstCard, FlashcardReviewRating.Good);
                AddReview(SecondCard, FlashcardReviewRating.Good);
                AddReview(SecondCard, FlashcardReviewRating.Good);
                break;
            case "cards disappeared":
                flashcards.SetupSequence(s => s.GetStudyCardsAsync(DeckId, OwnerId, Ct))
                    .ReturnsAsync(cards)
                    .ReturnsAsync(Array.Empty<FlashcardDto>());
                break;
            case "question unavailable":
                flashcards.Setup(s => s.GetLearnQuestionAsync(DeckId, OwnerId, FirstCard, Ct))
                    .ReturnsAsync((FlashcardLearnQuestionDto?)null);
                break;
        }

        Assert.Null(await Service.GetOrIssueQuestionAsync(SessionId, DeckId, OwnerId, FirstCard));
        Assert.Null(session.QuestionToken);
        repository.Verify(r => r.SaveChangesAsync(Ct), Times.Never);
    }

    [Theory]
    [InlineData("wrong turn", "Card không thuộc lượt học hiện tại.")]
    [InlineData("missing token", "Trạng thái câu hỏi không hợp lệ")]
    [InlineData("missing options", "Trạng thái câu hỏi không hợp lệ")]
    [InlineData("missing card", "Deck đã thay đổi. Hãy bắt đầu phiên mới")]
    [InlineData("null JSON", "Không thể khôi phục đáp án")]
    public async Task IssueQuestion_RejectsInconsistentPendingQuestions(string cause, string expected)
    {
        var session = AddSession(withQuestion: true);
        var requestedCard = FirstCard;
        switch (cause)
        {
            case "wrong turn":
                requestedCard = SecondCard;
                break;
            case "missing token":
                session.QuestionToken = null;
                break;
            case "missing options":
                session.QuestionOptionsJson = "   ";
                break;
            case "missing card":
                requestedCard = 99;
                session.CurrentCardId = requestedCard;
                break;
            case "null JSON":
                session.QuestionOptionsJson = "null";
                break;
        }

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Service.GetOrIssueQuestionAsync(SessionId, DeckId, OwnerId, requestedCard));

        Assert.Equal(expected, error.Message);
        repository.Verify(r => r.SaveChangesAsync(Ct), Times.Never);
    }

    [Fact]
    public async Task IssueQuestion_PersistsAndRestoresSameTokenAndOptions()
    {
        var session = AddSession();
        var question = SetupNewQuestion();

        var issued = Assert.IsType<LearnSessionQuestionDto>(
            await Service.GetOrIssueQuestionAsync(SessionId, DeckId, OwnerId, FirstCard));
        var restored = Assert.IsType<LearnSessionQuestionDto>(
            await Service.GetOrIssueQuestionAsync(SessionId, DeckId, OwnerId, FirstCard));

        Assert.Equal(SessionId, issued.SessionId);
        Assert.NotEqual(Guid.Empty, issued.QuestionToken);
        Assert.Equal(issued.QuestionToken, restored.QuestionToken);
        Assert.Equal(FirstCard, session.CurrentCardId);
        Assert.Equal(issued.QuestionToken, session.QuestionToken);
        Assert.Equal(question.Options, JsonSerializer.Deserialize<List<FlashcardLearnOptionDto>>(session.QuestionOptionsJson!));
        Assert.Equal(question.QuestionCardId, restored.Question.QuestionCardId);
        Assert.Equal(question.QuestionText, restored.Question.QuestionText);
        Assert.Equal(question.Options, restored.Question.Options);
        repository.Verify(r => r.SaveChangesAsync(Ct), Times.Once);
        flashcards.Verify(s => s.GetLearnQuestionAsync(DeckId, OwnerId, FirstCard, Ct), Times.Once);
    }

    [Theory]
    [InlineData("missing session", "Phiên học đã thay đổi. Hãy tải lại trang.")]
    [InlineData("wrong card", "Phiên học đã thay đổi. Hãy tải lại trang.")]
    [InlineData("missing token", "Phiên học đã thay đổi. Hãy tải lại trang.")]
    [InlineData("missing options", "Phiên học đã thay đổi. Hãy tải lại trang.")]
    [InlineData("completed", "Phiên học đã thay đổi. Hãy tải lại trang.")]
    [InlineData("invalidated", "Phiên học đã thay đổi. Hãy tải lại trang.")]
    [InlineData("null options", "Không thể khôi phục đáp án. Hãy tải lại trang.")]
    [InlineData("valid", null)]
    public async Task IssueQuestion_RecoversOnlyValidConcurrentSave(string concurrentState, string? expectedError)
    {
        AddSession();
        var question = SetupNewQuestion();
        FlashcardLearnSession? saved = new()
        {
            SessionId = SessionId,
            UserId = OwnerId,
            DeckId = DeckId,
            CurrentCardId = FirstCard,
            QuestionToken = Token,
            QuestionOptionsJson = """[{"Text":"Beta"},{"Text":"Alpha"}]"""
        };
        switch (concurrentState)
        {
            case "missing session": saved = null; break;
            case "wrong card": saved.CurrentCardId = SecondCard; break;
            case "missing token": saved.QuestionToken = null; break;
            case "missing options": saved.QuestionOptionsJson = ""; break;
            case "completed": saved.CompletedAtUtc = SeedTime; break;
            case "invalidated": saved.InvalidatedAtUtc = SeedTime; break;
            case "null options": saved.QuestionOptionsJson = "null"; break;
        }
        repository.Setup(r => r.SaveChangesAsync(Ct)).ThrowsAsync(new DbUpdateConcurrencyException());
        repository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<FlashcardLearnSession, bool>>>(), Ct))
            .ReturnsAsync((Expression<Func<FlashcardLearnSession, bool>> filter, CancellationToken _) =>
                saved is not null && filter.Compile()(saved) ? saved : null);

        if (expectedError is not null)
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => Service.GetOrIssueQuestionAsync(SessionId, DeckId, OwnerId, FirstCard));
            Assert.Equal(expectedError, error.Message);
            return;
        }

        var restored = Assert.IsType<LearnSessionQuestionDto>(
            await Service.GetOrIssueQuestionAsync(SessionId, DeckId, OwnerId, FirstCard));
        Assert.Equal(SessionId, restored.SessionId);
        Assert.Equal(Token, restored.QuestionToken);
        Assert.Equal(FirstCard, restored.Question.QuestionCardId);
        Assert.Equal(question.QuestionText, restored.Question.QuestionText);
        Assert.Equal(new[] { "Beta", "Alpha" }, restored.Question.Options.Select(o => o.Text));
    }

    [Fact]
    public async Task SubmitAnswer_NullRequestThrowsBeforeOpeningTransaction()
    {
        await Assert.ThrowsAsync<ArgumentNullException>("request",
            () => Service.SubmitAnswerAsync(SessionId, DeckId, OwnerId, Token, null!));

        repository.Verify(r => r.BeginTransactionAsync(Ct), Times.Never);
    }

    [Theory]
    [InlineData("duplicate", "Lượt trả lời đã được ghi nhận. Hãy tải lại trang.")]
    [InlineData("changed", "Bộ thẻ đã thay đổi. Hãy bắt đầu phiên mới")]
    [InlineData("empty token", "Câu hỏi đã hết hiệu lực")]
    [InlineData("wrong token", "Câu hỏi đã hết hiệu lực")]
    [InlineData("no stored token", "Câu hỏi đã hết hiệu lực")]
    [InlineData("wrong card", "Câu hỏi đã hết hiệu lực")]
    [InlineData("no stored card", "Câu hỏi đã hết hiệu lực")]
    [InlineData("no issued options", "Câu hỏi chưa được phát")]
    [InlineData("null options", "Đáp án không hợp lệ")]
    [InlineData("missing answer", "Đáp án không hợp lệ")]
    [InlineData("blank answer", "Đáp án không hợp lệ")]
    [InlineData("foreign option", "Đáp án không hợp lệ")]
    [InlineData("no grade", "Deck đã thay đổi. Hãy bắt đầu phiên mới")]
    [InlineData("correct option changed", "Deck đã thay đổi. Hãy bắt đầu phiên mới")]
    [InlineData("changed during grading", "Deck đã thay đổi. Hãy bắt đầu phiên mới")]
    public async Task SubmitAnswer_RejectsInconsistentRequestWithoutCommitting(string cause, string expected)
    {
        var session = AddSession(withQuestion: true);
        var token = Token;
        var cardId = FirstCard;
        string? selected = "Alpha";
        switch (cause)
        {
            case "duplicate":
                AddReview(FirstCard, FlashcardReviewRating.Good).RequestId = Token;
                break;
            case "changed":
                var changed = Card(FirstCard);
                changed.UpdatedAtUtc = SeedTime.AddMinutes(1);
                storedCards.Add(changed);
                break;
            case "empty token":
                token = Guid.Empty;
                break;
            case "wrong token":
                token = Guid.NewGuid();
                break;
            case "no stored token":
                session.QuestionToken = null;
                break;
            case "wrong card":
                cardId = SecondCard;
                break;
            case "no stored card":
                session.CurrentCardId = null;
                cardId = 0;
                break;
            case "no issued options":
                session.QuestionOptionsJson = null;
                break;
            case "null options":
                session.QuestionOptionsJson = "null";
                break;
            case "missing answer":
                selected = null;
                break;
            case "blank answer":
                selected = " ";
                break;
            case "foreign option":
                selected = "Gamma";
                break;
            case "no grade":
                SetupGrade(selected, null);
                break;
            case "correct option changed":
                SetupGrade(selected, new(FirstCard, selected, "Gamma", false));
                break;
            case "changed during grading":
                SetupGrade(selected, new(FirstCard, selected, "Alpha", true));
                repository.SetupSequence(r => r.AnyAsync(It.IsAny<Expression<Func<Flashcard, bool>>>(), Ct))
                    .ReturnsAsync(false)
                    .ReturnsAsync(true);
                break;
        }
        var storedToken = session.QuestionToken;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Service.SubmitAnswerAsync(SessionId, DeckId, OwnerId, token, new(cardId, selected)));

        Assert.Equal(expected, error.Message);
        Assert.Equal(storedToken, session.QuestionToken);
        repository.Verify(r => r.AddAsync(It.IsAny<FlashcardReview>(), Ct), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(Ct), Times.Never);
        transaction.Verify(t => t.CommitAsync(Ct), Times.Never);
        transaction.Verify(t => t.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task SubmitAnswer_RecordsIncorrectAttemptAndCreatesProgress()
    {
        var session = AddSession(withQuestion: true);
        var graded = new FlashcardLearnAnswerResultDto(FirstCard, "bEtA", "Alpha", false);
        SetupGrade("  bEtA ", graded);
        AddReview(FirstCard, FlashcardReviewRating.Good);
        AddReview(SecondCard, FlashcardReviewRating.Good).RequestId = Token;
        AddReview(FirstCard, FlashcardReviewRating.Good).SessionId = Guid.NewGuid();
        reviews[^1].RequestId = Token;
        AddReview(FirstCard, FlashcardReviewRating.Good).UserId = OtherUserId;
        reviews[^1].RequestId = Token;

        Assert.Equal(graded, await Service.SubmitAnswerAsync(SessionId, DeckId, OwnerId, Token, new(FirstCard, "  bEtA ")));

        var review = reviews[^1];
        Assert.Equal(Token, review.RequestId);
        Assert.Equal(SessionId, review.SessionId);
        Assert.Equal(FirstCard, review.FlashcardId);
        Assert.Equal(OwnerId, review.UserId);
        Assert.Equal((byte)FlashcardReviewRating.Again, review.Rating);
        Assert.NotNull(progress);
        Assert.Equal(2.50m, progress.EaseFactor);
        Assert.Equal(review.ReviewedAtUtc, progress.NextReviewAtUtc);
        Assert.Same(progress, review.FlashcardProgress);
        Assert.Null(session.CurrentCardId);
        Assert.Null(session.QuestionToken);
        Assert.Null(session.QuestionOptionsJson);
        Assert.Null(session.CompletedAtUtc);
        repository.Verify(r => r.SaveChangesAsync(Ct), Times.Once);
        transaction.Verify(t => t.CommitAsync(Ct), Times.Once);
        transaction.Verify(t => t.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task SubmitAnswer_CompletesSessionWithSnapshotAndRejectsRetry()
    {
        var session = AddSession(withQuestion: true);
        var existingProgress = new FlashcardProgress { UserId = OwnerId, FlashcardId = FirstCard };
        progress = existingProgress;
        AddReview(FirstCard, FlashcardReviewRating.Good);
        AddReview(SecondCard, FlashcardReviewRating.Good);
        AddReview(SecondCard, FlashcardReviewRating.Good);
        var graded = new FlashcardLearnAnswerResultDto(FirstCard, "Alpha", "Alpha", true);
        SetupGrade("Alpha", graded);

        Assert.Equal(graded, await Service.SubmitAnswerAsync(SessionId, DeckId, OwnerId, Token, new(FirstCard, "Alpha")));

        Assert.NotNull(session.CompletedAtUtc);
        Assert.Equal(2, session.CompletedTotalCards);
        Assert.Same(existingProgress, reviews[^1].FlashcardProgress);
        Assert.Equal((byte)FlashcardReviewRating.Good, reviews[^1].Rating);
        repository.Verify(r => r.AddAsync(It.IsAny<FlashcardProgress>(), Ct), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(Ct), Times.Exactly(2));
        Assert.Equal(new LearnResultDto(SessionId, DeckId, 4, 4, 0, 100, 2, 2, 0, true),
            await Service.GetResultAsync(SessionId, DeckId, OwnerId));

        var retry = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Service.SubmitAnswerAsync(SessionId, DeckId, OwnerId, Token, new(FirstCard, "Alpha")));
        Assert.Equal("Lượt trả lời đã được ghi nhận. Hãy tải lại trang.", retry.Message);
        Assert.Equal(4, reviews.Count);
        transaction.Verify(t => t.CommitAsync(Ct), Times.Once);
    }

    [Fact]
    public async Task SubmitAnswer_DoesNotMarkSessionCompleteWhenStateIsUnavailableAfterSave()
    {
        var session = AddSession(withQuestion: true);
        var graded = new FlashcardLearnAnswerResultDto(FirstCard, "Alpha", "Alpha", true);
        SetupGrade("Alpha", graded);
        repository.SetupSequence(r => r.FindAsync<FlashcardLearnSession>(It.IsAny<object[]>(), Ct))
            .Returns(ValueTask.FromResult<FlashcardLearnSession?>(session))
            .Returns(ValueTask.FromResult<FlashcardLearnSession?>(null));

        Assert.Equal(graded, await Service.SubmitAnswerAsync(SessionId, DeckId, OwnerId, Token, new(FirstCard, "Alpha")));

        Assert.Null(session.CompletedAtUtc);
        Assert.Null(session.CompletedTotalCards);
        Assert.Single(reviews);
        repository.Verify(r => r.SaveChangesAsync(Ct), Times.Once);
        transaction.Verify(t => t.CommitAsync(Ct), Times.Once);
    }

    private FlashcardLearnQuestionDto SetupNewQuestion()
    {
        var question = new FlashcardLearnQuestionDto(FirstCard, "First", [new("Alpha"), new("Beta")]);
        flashcards.Setup(s => s.GetLearnQuestionAsync(DeckId, OwnerId, FirstCard, Ct)).ReturnsAsync(question);
        return question;
    }

    private void SetupGrade(string selection, FlashcardLearnAnswerResultDto? result) =>
        flashcards.Setup(s => s.GradeLearnAnswerAsync(
            DeckId, OwnerId, new SubmitFlashcardLearnAnswerRequestDto(FirstCard, selection), Ct))
            .ReturnsAsync(result);
}
