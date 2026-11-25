using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using StudyHub.BLL.DTOs.Flashcards;
using StudyHub.Web.Controllers.Flashcards;
using StudyHub.Web.ViewModels.Flashcards;

namespace StudyHub.Flashcards.Tests;

public sealed partial class FlashcardsControllerTests
{
    private const string SubmittedAnswer = "  Merge rows  ";
    private static readonly Guid SessionId = new("db34bfb5-7963-454a-b8cd-cef0aaa54bf9");
    private static readonly Guid QuestionToken = new("047ff542-4525-4537-86fe-d380b2e6dff5");

    [Fact]
    public async Task Learn_displays_setup_with_the_deck_identity()
    {
        var deck = AvailableDeck();

        var view = Assert.IsType<ViewResult>(await controller.Learn(DeckId, Token));

        Assert.Equal("LearnSetup", view.ViewName);
        var model = Assert.IsType<FlashcardLearnViewModel>(view.Model);
        Assert.Equal(deck.DeckId, model.DeckId);
        Assert.Equal(deck.Title, model.DeckTitle);
        Assert.Null(model.Session);
        Assert.Null(model.Question);
        Assert.Null(model.Result);
    }

    [Fact]
    public async Task Learn_returns_not_found_for_an_unavailable_deck()
    {
        decks.Setup(service => service.GetDeckDetailAsync(DeckId, UserId, Token))
            .ReturnsAsync((FlashcardDeckDetailDto?)null);

        Assert.IsType<NotFoundResult>(await controller.Learn(DeckId, Token));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StartLearn_passes_the_shuffle_preference_and_redirects_to_the_session(bool shuffle)
    {
        learning.Setup(service => service.StartOrResumeAsync(DeckId, UserId, Token, shuffle))
            .ReturnsAsync(SessionId);

        var result = await controller.StartLearn(DeckId, shuffle, Token);

        var redirect = AssertRedirect(result, nameof(FlashcardsController.LearnSession), DeckId);
        Assert.NotNull(redirect.RouteValues);
        Assert.Equal(SessionId, redirect.RouteValues["sessionId"]);
    }

    [Fact]
    public async Task StartLearn_returns_to_setup_with_an_error_when_session_cannot_start()
    {
        learning.Setup(service => service.StartOrResumeAsync(DeckId, UserId, Token, true))
            .ReturnsAsync((Guid?)null);

        var result = await controller.StartLearn(DeckId, true, Token);

        AssertRedirect(result, nameof(FlashcardsController.Learn), DeckId);
        Assert.Equal("Không thể bắt đầu phiên học.", controller.TempData[ErrorKey]);
    }

    [Fact]
    public async Task LearnSession_returns_not_found_before_loading_a_session_for_an_unavailable_deck()
    {
        decks.Setup(service => service.GetDeckDetailAsync(DeckId, UserId, Token))
            .ReturnsAsync((FlashcardDeckDetailDto?)null);

        var result = await controller.LearnSession(DeckId, SessionId, Token);

        Assert.IsType<NotFoundResult>(result);
        learning.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task LearnSession_returns_not_found_when_session_state_is_unavailable()
    {
        AvailableDeck();
        learning.Setup(service => service.GetStateAsync(SessionId, DeckId, UserId, Token))
            .ReturnsAsync((DbLearnStateDto?)null);

        Assert.IsType<NotFoundResult>(await controller.LearnSession(DeckId, SessionId, Token));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LearnSession_uses_the_issued_question_for_the_next_card(bool questionIssued)
    {
        var deck = AvailableDeck();
        var session = new LearnSessionInfoDto(SessionId, DeckId, 6, 2, 1, 3, false, true);
        var question = questionIssued
            ? new LearnSessionQuestionDto(SessionId, QuestionToken, new FlashcardLearnQuestionDto(
                CardId, "JOIN", [new("Combine rows"), new("Filter rows")]))
            : null;
        learning.Setup(service => service.GetStateAsync(SessionId, DeckId, UserId, Token))
            .ReturnsAsync(new DbLearnStateDto(session, CardId));
        learning.Setup(service => service.GetOrIssueQuestionAsync(SessionId, DeckId, UserId, CardId, Token))
            .ReturnsAsync(question).Verifiable(Times.Once());

        var view = Assert.IsType<ViewResult>(await controller.LearnSession(DeckId, SessionId, Token));

        Assert.Equal("Learn", view.ViewName);
        var model = Assert.IsType<FlashcardLearnViewModel>(view.Model);
        Assert.Equal(deck.DeckId, model.DeckId);
        Assert.Equal(deck.Title, model.DeckTitle);
        Assert.Same(session, model.Session);
        Assert.Equal(question, model.Question);
        Assert.Null(model.Result);
        learning.Verify();
    }

    [Fact]
    public async Task LearnSession_displays_an_unfinished_session_with_no_next_card()
    {
        AvailableDeck();
        var session = new LearnSessionInfoDto(SessionId, DeckId, 6, 2, 1, 3, false);
        learning.Setup(service => service.GetStateAsync(SessionId, DeckId, UserId, Token))
            .ReturnsAsync(new DbLearnStateDto(session, null));

        var view = Assert.IsType<ViewResult>(await controller.LearnSession(DeckId, SessionId, Token));

        Assert.Equal("Learn", view.ViewName);
        var model = Assert.IsType<FlashcardLearnViewModel>(view.Model);
        Assert.Same(session, model.Session);
        Assert.Null(model.Question);
        Assert.Null(model.Result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LearnSession_loads_the_result_for_a_completed_session(bool resultAvailable)
    {
        AvailableDeck();
        var session = new LearnSessionInfoDto(SessionId, DeckId, 6, 0, 0, 6, true);
        var result = resultAvailable ? new LearnResultDto(SessionId, DeckId, 9, 8, 1, 88.9, 6, 6, 0, true) : null;
        learning.Setup(service => service.GetStateAsync(SessionId, DeckId, UserId, Token))
            .ReturnsAsync(new DbLearnStateDto(session, CardId));
        learning.Setup(service => service.GetResultAsync(SessionId, DeckId, UserId, Token))
            .ReturnsAsync(result).Verifiable(Times.Once());

        var view = Assert.IsType<ViewResult>(await controller.LearnSession(DeckId, SessionId, Token));

        Assert.Equal("Learn", view.ViewName);
        var model = Assert.IsType<FlashcardLearnViewModel>(view.Model);
        Assert.Same(session, model.Session);
        Assert.Null(model.Question);
        Assert.Equal(result, model.Result);
        learning.Verify();
    }

    [Fact]
    public async Task LearnSession_redirects_to_setup_with_the_session_service_error()
    {
        AvailableDeck();
        const string message = "Bộ thẻ đã thay đổi. Hãy bắt đầu phiên mới";
        var session = new LearnSessionInfoDto(SessionId, DeckId, 6, 2, 1, 3, false);
        learning.Setup(service => service.GetStateAsync(SessionId, DeckId, UserId, Token))
            .ReturnsAsync(new DbLearnStateDto(session, CardId));
        learning.Setup(service => service.GetOrIssueQuestionAsync(SessionId, DeckId, UserId, CardId, Token))
            .ThrowsAsync(new InvalidOperationException(message));

        var result = await controller.LearnSession(DeckId, SessionId, Token);

        AssertRedirect(result, nameof(FlashcardsController.Learn), DeckId);
        Assert.Equal(message, controller.TempData[ErrorKey]);
    }

    [Fact]
    public async Task LearnSession_redirects_to_setup_when_saving_a_question_has_a_concurrency_conflict()
    {
        AvailableDeck();
        var session = new LearnSessionInfoDto(SessionId, DeckId, 6, 2, 1, 3, false);
        learning.Setup(service => service.GetStateAsync(SessionId, DeckId, UserId, Token))
            .ReturnsAsync(new DbLearnStateDto(session, CardId));
        learning.Setup(service => service.GetOrIssueQuestionAsync(SessionId, DeckId, UserId, CardId, Token))
            .ThrowsAsync(new DbUpdateConcurrencyException("Concurrent question update"));

        var result = await controller.LearnSession(DeckId, SessionId, Token);

        AssertRedirect(result, nameof(FlashcardsController.Learn), DeckId);
        Assert.Equal("Phiên học đã thay đổi. Hãy bắt đầu hoặc tiếp tục lại.", controller.TempData[ErrorKey]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SubmitLearnAnswer_returns_bad_request_for_invalid_model_state_or_missing_token(bool invalidModelState)
    {
        var model = AnswerForm();
        if (invalidModelState)
        {
            controller.ModelState.AddModelError(nameof(model.SelectedAnswer), "Required");
        }
        else
        {
            model.QuestionToken = Guid.Empty;
        }

        var result = await controller.SubmitLearnAnswer(DeckId, SessionId, model, Token);

        AssertMessage<BadRequestObjectResult>(
            result, StatusCodes.Status400BadRequest, "Dữ liệu câu trả lời không hợp lệ");
        learning.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SubmitLearnAnswer_returns_not_found_with_a_message_when_answer_submission_has_no_session()
    {
        var request = new SubmitFlashcardLearnAnswerRequestDto(CardId, SubmittedAnswer);
        learning.Setup(service => service.SubmitAnswerAsync(SessionId, DeckId, UserId, QuestionToken, request, Token))
            .ReturnsAsync((FlashcardLearnAnswerResultDto?)null);

        var result = await controller.SubmitLearnAnswer(DeckId, SessionId, AnswerForm(), Token);

        AssertMessage<NotFoundObjectResult>(result, StatusCodes.Status404NotFound, "Phiên học không tồn tại.");
    }

    [Fact]
    public async Task SubmitLearnAnswer_returns_not_found_when_state_disappears_after_saving_the_answer()
    {
        var request = new SubmitFlashcardLearnAnswerRequestDto(CardId, SubmittedAnswer);
        learning.Setup(service => service.SubmitAnswerAsync(SessionId, DeckId, UserId, QuestionToken, request, Token))
            .ReturnsAsync(new FlashcardLearnAnswerResultDto(CardId, "Merge rows", "Combine rows", false));
        learning.Setup(service => service.GetStateAsync(SessionId, DeckId, UserId, Token))
            .ReturnsAsync((DbLearnStateDto?)null);

        var result = await controller.SubmitLearnAnswer(DeckId, SessionId, AnswerForm(), Token);

        Assert.IsType<NotFoundResult>(result);
    }

    [Theory]
    [InlineData(false, 2, 3, 1, false)]
    [InlineData(true, 0, 0, 6, true)]
    public async Task SubmitLearnAnswer_returns_grade_and_new_progress_after_saving_the_answer(
        bool isCorrect, int newCount, int learningCount, int learnedCount, bool isCompleted)
    {
        var request = new SubmitFlashcardLearnAnswerRequestDto(CardId, SubmittedAnswer);
        var sequence = new MockSequence();
        learning.InSequence(sequence)
            .Setup(service => service.SubmitAnswerAsync(SessionId, DeckId, UserId, QuestionToken, request, Token))
            .ReturnsAsync(new FlashcardLearnAnswerResultDto(
                CardId, "Merge rows", isCorrect ? "Merge rows" : "Combine rows", isCorrect));
        var progress = new LearnSessionInfoDto(SessionId, DeckId, 6, newCount, learningCount, learnedCount, isCompleted);
        learning.InSequence(sequence)
            .Setup(service => service.GetStateAsync(SessionId, DeckId, UserId, Token))
            .ReturnsAsync(new DbLearnStateDto(progress, null));

        var response = Assert.IsType<JsonResult>(
            await controller.SubmitLearnAnswer(DeckId, SessionId, AnswerForm(), Token));

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode ?? StatusCodes.Status200OK);
        var payload = JsonSerializer.SerializeToElement(response.Value);
        Assert.Equal(9, payload.EnumerateObject().Count());
        Assert.Equal(CardId, payload.GetProperty("questionCardId").GetInt64());
        Assert.Equal("Merge rows", payload.GetProperty("selectedAnswer").GetString());
        Assert.Equal(isCorrect ? "Merge rows" : "Combine rows", payload.GetProperty("correctAnswer").GetString());
        Assert.Equal(isCorrect, payload.GetProperty("isCorrect").GetBoolean());
        Assert.Equal(6, payload.GetProperty("totalCards").GetInt32());
        Assert.Equal(newCount, payload.GetProperty("newCount").GetInt32());
        Assert.Equal(learningCount, payload.GetProperty("learningCount").GetInt32());
        Assert.Equal(learnedCount, payload.GetProperty("learnedCount").GetInt32());
        Assert.Equal(isCompleted, payload.GetProperty("isCompleted").GetBoolean());
    }

    [Fact]
    public async Task SubmitLearnAnswer_returns_bad_request_for_argument_error()
    {
        const string message = "Câu trả lời không hợp lệ.";
        AnswerSubmissionFails(new ArgumentException(message));

        var result = await controller.SubmitLearnAnswer(DeckId, SessionId, AnswerForm(), Token);

        AssertMessage<BadRequestObjectResult>(result, StatusCodes.Status400BadRequest, message);
    }

    [Fact]
    public async Task SubmitLearnAnswer_returns_conflict_for_an_expired_question()
    {
        const string message = "Câu hỏi đã hết hiệu lực";
        AnswerSubmissionFails(new InvalidOperationException(message));

        var result = await controller.SubmitLearnAnswer(DeckId, SessionId, AnswerForm(), Token);

        AssertMessage<ConflictObjectResult>(result, StatusCodes.Status409Conflict, message);
    }

    [Fact]
    public async Task SubmitLearnAnswer_returns_conflict_for_a_concurrent_update()
    {
        AnswerSubmissionFails(new DbUpdateConcurrencyException("Concurrent answer update"));

        var result = await controller.SubmitLearnAnswer(DeckId, SessionId, AnswerForm(), Token);

        AssertMessage<ConflictObjectResult>(
            result, StatusCodes.Status409Conflict, "Phiên học đã thay đổi. Hãy tải lại trang.");
    }

    [Theory]
    [InlineData(2601)]
    [InlineData(2627)]
    public async Task SubmitLearnAnswer_returns_conflict_when_sql_reports_a_duplicate_answer(int errorNumber)
    {
        AnswerSubmissionFails(new DbUpdateException(
            "Could not save answer", SqlExceptionFactory.Create(errorNumber, "Duplicate answer")));

        var result = await controller.SubmitLearnAnswer(DeckId, SessionId, AnswerForm(), Token);

        AssertMessage<ConflictObjectResult>(
            result, StatusCodes.Status409Conflict, "Lượt trả lời đã được ghi nhận. Hãy tải lại trang.");
    }

    [Theory]
    [InlineData(547)]
    [InlineData(null)]
    public async Task SubmitLearnAnswer_propagates_database_failures_unrelated_to_duplicate_answers(int? errorNumber)
    {
        var innerFailure = errorNumber is { } number
            ? SqlExceptionFactory.Create(number, "Database constraint failure")
            : new Exception("Persistence failure");
        var failure = new DbUpdateException("Could not save answer", innerFailure);
        AnswerSubmissionFails(failure);

        var thrown = await Assert.ThrowsAsync<DbUpdateException>(
            () => controller.SubmitLearnAnswer(DeckId, SessionId, AnswerForm(), Token));

        Assert.Same(failure, thrown);
    }

    private static FlashcardLearnAnswerFormViewModel AnswerForm() => new()
    {
        QuestionCardId = CardId,
        QuestionToken = QuestionToken,
        SelectedAnswer = SubmittedAnswer
    };

    private void AnswerSubmissionFails(Exception failure)
    {
        var request = new SubmitFlashcardLearnAnswerRequestDto(CardId, SubmittedAnswer);
        learning.Setup(service => service.SubmitAnswerAsync(SessionId, DeckId, UserId, QuestionToken, request, Token))
            .ThrowsAsync(failure);
    }

    private static void AssertMessage<TResult>(IActionResult result, int statusCode, string expectedMessage)
        where TResult : ObjectResult
    {
        var response = Assert.IsType<TResult>(result);
        Assert.Equal(statusCode, response.StatusCode);
        var payload = JsonSerializer.SerializeToElement(response.Value);
        Assert.Single(payload.EnumerateObject());
        Assert.Equal(expectedMessage, payload.GetProperty("message").GetString());
    }
}
