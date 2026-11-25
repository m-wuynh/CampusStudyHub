using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using StudyHub.BLL.DTOs.Flashcards;
using StudyHub.BLL.Services.Auth;
using StudyHub.BLL.Services.Flashcards.Interfaces;
using StudyHub.Web.Controllers.Flashcards;
using StudyHub.Web.ViewModels.Flashcards;

namespace StudyHub.Flashcards.Tests;

public sealed partial class FlashcardsControllerTests : IDisposable
{
    private const long UserId = 29;
    private const long DeckId = 53;
    private const long CardId = 71;
    private const long SubjectId = 83;
    private const string ErrorKey = "FlashcardError";
    private const string SuccessKey = "FlashcardSuccess";

    private readonly Mock<IFlashcardDeckService> decks = new(MockBehavior.Strict);
    private readonly Mock<IFlashcardService> cards = new(MockBehavior.Strict);
    private readonly Mock<IDbLearnSessionService> learning = new(MockBehavior.Strict);
    private readonly CancellationTokenSource cancellation = new();
    private readonly FlashcardsController controller;

    private CancellationToken Token => cancellation.Token;

    public FlashcardsControllerTests()
    {
        var currentUser = new Mock<ICurrentUser>(MockBehavior.Strict);
        currentUser.SetupGet(user => user.UserId).Returns(UserId);
        var context = new DefaultHttpContext();
        var tempDataProvider = new Mock<ITempDataProvider>(MockBehavior.Strict);
        tempDataProvider.Setup(provider => provider.LoadTempData(context))
            .Returns(new Dictionary<string, object>());

        controller = new FlashcardsController(decks.Object, cards.Object, learning.Object, currentUser.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
            TempData = new TempDataDictionary(context, tempDataProvider.Object)
        };
    }

    [Theory]
    [InlineData(null, new long[] { 1, 2, 3 })]
    [InlineData("   ", new long[] { 1, 2, 3 })]
    [InlineData("  sQl  ", new long[] { 1, 3 })]
    [InlineData("chemistry", new long[] { })]
    public async Task Index_filters_titles_and_counts_all_decks(string? search, long[] expectedDeckIds)
    {
        var now = DateTime.UtcNow;
        var recent = now.AddDays(-7).AddHours(1);
        var older = now.AddDays(-7).AddHours(-1);
        FlashcardDeckSummaryDto[] ownedDecks =
        [
            new(1, "SQL joins", null, "Private", recent, recent, SubjectId, "Databases"),
            new(2, "C# syntax", "SQL examples", "Private", older, recent, null, null),
            new(3, "Intro to sql", null, "Private", older, older, SubjectId, "Databases")
        ];
        FlashcardSubjectOptionDto[] subjects = [new(SubjectId, "Databases")];
        decks.Setup(service => service.GetMyDecksAsync(UserId, Token)).ReturnsAsync(ownedDecks);
        decks.Setup(service => service.GetSubjectOptionsAsync(UserId, Token)).ReturnsAsync(subjects);

        var view = Assert.IsType<ViewResult>(await controller.Index(search, Token));

        var model = Assert.IsType<FlashcardsIndexViewModel>(view.Model);
        Assert.Equal(expectedDeckIds, model.Decks.Select(deck => deck.DeckId));
        Assert.Equal(search, model.SearchText);
        Assert.Equal(3, model.TotalDecks);
        Assert.Equal(1, model.NewDecksLast7Days);
        Assert.Equal(2, model.UpdatedDecksLast7Days);
        Assert.Same(subjects, model.SubjectOptions);
    }

    [Fact]
    public async Task Create_passes_the_chosen_subject_and_redirects_to_index()
    {
        var model = new FlashcardDeckFormViewModel
        {
            Title = "SQL",
            Description = "Query practice",
            UserSubjectId = SubjectId
        };
        var request = new CreateFlashcardDeckRequestDto("SQL", "Query practice", SubjectId);
        decks.Setup(service => service.CreateDeckAsync(UserId, request, Token))
            .ReturnsAsync(DeckId).Verifiable(Times.Once());

        var result = await controller.Create(model, Token);

        AssertRedirect(result, nameof(FlashcardsController.Index));
        Assert.Equal("Tạo bộ flashcard thành công", controller.TempData[SuccessKey]);
        decks.Verify();
    }

    [Fact]
    public async Task Create_rejects_invalid_model_state_without_writing_a_deck()
    {
        controller.ModelState.AddModelError(nameof(FlashcardDeckFormViewModel.Title), "Required");

        var result = await controller.Create(new FlashcardDeckFormViewModel(), Token);

        AssertRedirect(result, nameof(FlashcardsController.Index));
        Assert.Equal("Dữ liệu bộ flashcard không hợp lệ", controller.TempData[ErrorKey]);
        decks.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Create_shows_validation_error_from_the_deck_service()
    {
        const string message = "Vui lòng chọn môn học.";
        var model = new FlashcardDeckFormViewModel { Title = "SQL" };
        var request = new CreateFlashcardDeckRequestDto("SQL", null, null);
        decks.Setup(service => service.CreateDeckAsync(UserId, request, Token))
            .ThrowsAsync(new ArgumentException(message));

        var result = await controller.Create(model, Token);

        AssertRedirect(result, nameof(FlashcardsController.Index));
        Assert.Equal(message, controller.TempData[ErrorKey]);
    }

    [Fact]
    public async Task Edit_updates_the_route_deck_title_and_description()
    {
        var model = new FlashcardDeckFormViewModel
        {
            DeckId = DeckId + 1,
            Title = "Advanced SQL",
            Description = "More queries",
            UserSubjectId = SubjectId
        };
        var request = new UpdateFlashcardDeckRequestDto("Advanced SQL", "More queries");
        decks.Setup(service => service.UpdateDeckAsync(DeckId, UserId, request, Token))
            .ReturnsAsync(true).Verifiable(Times.Once());

        var result = await controller.Edit(DeckId, model, Token);

        AssertRedirect(result, nameof(FlashcardsController.Index));
        Assert.Equal("Cập nhật bộ flashcard thành công", controller.TempData[SuccessKey]);
        decks.Verify();
    }

    [Fact]
    public async Task Edit_rejects_invalid_model_state_without_writing_a_deck()
    {
        controller.ModelState.AddModelError(nameof(FlashcardDeckFormViewModel.Title), "Required");

        var result = await controller.Edit(DeckId, new FlashcardDeckFormViewModel(), Token);

        AssertRedirect(result, nameof(FlashcardsController.Index));
        Assert.Equal("Dữ liệu bộ flashcard không hợp lệ", controller.TempData[ErrorKey]);
        decks.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Edit_returns_not_found_when_the_deck_service_cannot_update()
    {
        var model = new FlashcardDeckFormViewModel { Title = "SQL" };
        var request = new UpdateFlashcardDeckRequestDto("SQL", null);
        decks.Setup(service => service.UpdateDeckAsync(DeckId, UserId, request, Token)).ReturnsAsync(false);

        var result = await controller.Edit(DeckId, model, Token);

        Assert.IsType<NotFoundResult>(result);
        Assert.False(controller.TempData.ContainsKey(SuccessKey));
    }

    [Fact]
    public async Task Edit_shows_validation_error_from_the_deck_service()
    {
        const string message = "Tên bộ thẻ không hợp lệ.";
        var model = new FlashcardDeckFormViewModel { Title = " " };
        var request = new UpdateFlashcardDeckRequestDto(" ", null);
        decks.Setup(service => service.UpdateDeckAsync(DeckId, UserId, request, Token))
            .ThrowsAsync(new ArgumentException(message));

        var result = await controller.Edit(DeckId, model, Token);

        AssertRedirect(result, nameof(FlashcardsController.Index));
        Assert.Equal(message, controller.TempData[ErrorKey]);
    }

    [Fact]
    public async Task Delete_removes_the_deck_and_redirects_to_index()
    {
        decks.Setup(service => service.DeleteDeckAsync(DeckId, UserId, Token))
            .ReturnsAsync(true).Verifiable(Times.Once());

        var result = await controller.Delete(DeckId, Token);

        AssertRedirect(result, nameof(FlashcardsController.Index));
        Assert.Equal("Xóa bộ flashcard thành công", controller.TempData[SuccessKey]);
        decks.Verify();
    }

    [Fact]
    public async Task Delete_returns_not_found_when_the_deck_service_cannot_delete()
    {
        decks.Setup(service => service.DeleteDeckAsync(DeckId, UserId, Token)).ReturnsAsync(false);

        var result = await controller.Delete(DeckId, Token);

        Assert.IsType<NotFoundResult>(result);
        Assert.False(controller.TempData.ContainsKey(SuccessKey));
    }

    [Fact]
    public async Task Details_displays_the_deck_and_its_cards()
    {
        var deck = AvailableDeck();
        FlashcardDto[] deckCards = [new(CardId, "JOIN", "Combine rows", 6)];
        cards.Setup(service => service.GetCardsByDeckAsync(DeckId, UserId, Token)).ReturnsAsync(deckCards);

        var view = Assert.IsType<ViewResult>(await controller.Details(DeckId, Token));

        Assert.Equal("Details", view.ViewName);
        var model = Assert.IsType<FlashcardDeckDetailViewModel>(view.Model);
        Assert.Same(deck, model.Deck);
        Assert.Same(deckCards, model.Cards);
    }

    [Fact]
    public async Task Details_returns_not_found_before_loading_cards_for_an_unavailable_deck()
    {
        decks.Setup(service => service.GetDeckDetailAsync(DeckId, UserId, Token))
            .ReturnsAsync((FlashcardDeckDetailDto?)null);

        var result = await controller.Details(DeckId, Token);

        Assert.IsType<NotFoundResult>(result);
        cards.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateCard_lets_the_service_choose_sort_order_and_redirects_to_details()
    {
        var model = new FlashcardCardFormViewModel
        {
            DeckId = DeckId + 1,
            FrontText = "JOIN",
            BackText = "Combine rows",
            SortOrder = 6
        };
        var request = new CreateFlashcardRequestDto("JOIN", "Combine rows", null);
        cards.Setup(service => service.CreateCardAsync(DeckId, UserId, request, Token))
            .ReturnsAsync(CardId).Verifiable(Times.Once());

        var result = await controller.CreateCard(DeckId, model, Token);

        AssertRedirect(result, nameof(FlashcardsController.Details), DeckId);
        Assert.Equal("Tạo thẻ flashcard thành công", controller.TempData[SuccessKey]);
        cards.Verify();
    }

    [Fact]
    public async Task CreateCard_rejects_invalid_model_state_without_writing_a_card()
    {
        controller.ModelState.AddModelError(nameof(FlashcardCardFormViewModel.FrontText), "Required");

        var result = await controller.CreateCard(DeckId, new FlashcardCardFormViewModel(), Token);

        AssertRedirect(result, nameof(FlashcardsController.Details), DeckId);
        Assert.Equal("Dữ liệu thẻ flashcard không hợp lệ", controller.TempData[ErrorKey]);
        cards.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true, "Mặt trước không hợp lệ.")]
    [InlineData(false, "Bộ thẻ cần có môn học đang hoạt động.")]
    public async Task CreateCard_shows_validation_or_deck_error_from_the_service(bool invalidArgument, string message)
    {
        var model = new FlashcardCardFormViewModel { FrontText = "JOIN", BackText = "Combine rows" };
        var request = new CreateFlashcardRequestDto("JOIN", "Combine rows", null);
        Exception failure = invalidArgument ? new ArgumentException(message) : new InvalidOperationException(message);
        cards.Setup(service => service.CreateCardAsync(DeckId, UserId, request, Token)).ThrowsAsync(failure);

        var result = await controller.CreateCard(DeckId, model, Token);

        AssertRedirect(result, nameof(FlashcardsController.Details), DeckId);
        Assert.Equal(message, controller.TempData[ErrorKey]);
    }

    [Fact]
    public async Task EditCard_passes_updated_text_and_sort_order_for_the_route_card()
    {
        var model = new FlashcardCardFormViewModel
        {
            DeckId = DeckId + 1,
            FlashcardId = CardId + 1,
            FrontText = "LEFT JOIN",
            BackText = "Preserve unmatched left rows",
            SortOrder = 11
        };
        var request = new UpdateFlashcardRequestDto("LEFT JOIN", "Preserve unmatched left rows", 11);
        cards.Setup(service => service.UpdateCardAsync(DeckId, CardId, UserId, request, Token))
            .ReturnsAsync(true).Verifiable(Times.Once());

        var result = await controller.EditCard(DeckId, CardId, model, Token);

        AssertRedirect(result, nameof(FlashcardsController.Details), DeckId);
        Assert.Equal("Cập nhật thẻ flashcard thành công", controller.TempData[SuccessKey]);
        cards.Verify();
    }

    [Fact]
    public async Task EditCard_rejects_invalid_model_state_without_writing_a_card()
    {
        controller.ModelState.AddModelError(nameof(FlashcardCardFormViewModel.FrontText), "Required");

        var result = await controller.EditCard(DeckId, CardId, new FlashcardCardFormViewModel(), Token);

        AssertRedirect(result, nameof(FlashcardsController.Details), DeckId);
        Assert.Equal("Dữ liệu thẻ flashcard không hợp lệ", controller.TempData[ErrorKey]);
        cards.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EditCard_returns_not_found_when_the_service_cannot_update()
    {
        var model = new FlashcardCardFormViewModel { FrontText = "JOIN", BackText = "Combine rows" };
        var request = new UpdateFlashcardRequestDto("JOIN", "Combine rows", 0);
        cards.Setup(service => service.UpdateCardAsync(DeckId, CardId, UserId, request, Token))
            .ReturnsAsync(false);

        var result = await controller.EditCard(DeckId, CardId, model, Token);

        Assert.IsType<NotFoundResult>(result);
        Assert.False(controller.TempData.ContainsKey(SuccessKey));
    }

    [Fact]
    public async Task EditCard_shows_validation_error_from_the_service()
    {
        const string message = "Mặt trước không hợp lệ.";
        var model = new FlashcardCardFormViewModel { FrontText = " ", BackText = "Combine rows" };
        var request = new UpdateFlashcardRequestDto(" ", "Combine rows", 0);
        cards.Setup(service => service.UpdateCardAsync(DeckId, CardId, UserId, request, Token))
            .ThrowsAsync(new ArgumentException(message));

        var result = await controller.EditCard(DeckId, CardId, model, Token);

        AssertRedirect(result, nameof(FlashcardsController.Details), DeckId);
        Assert.Equal(message, controller.TempData[ErrorKey]);
    }

    [Fact]
    public async Task DeleteCard_deletes_the_route_card_and_redirects_to_details()
    {
        cards.Setup(service => service.DeleteCardAsync(DeckId, CardId, UserId, Token))
            .ReturnsAsync(true).Verifiable(Times.Once());

        var result = await controller.DeleteCard(DeckId, CardId, Token);

        AssertRedirect(result, nameof(FlashcardsController.Details), DeckId);
        Assert.Equal("Xóa thẻ flashcard thành công", controller.TempData[SuccessKey]);
        cards.Verify();
    }

    [Fact]
    public async Task DeleteCard_returns_not_found_when_the_service_cannot_delete()
    {
        cards.Setup(service => service.DeleteCardAsync(DeckId, CardId, UserId, Token)).ReturnsAsync(false);

        var result = await controller.DeleteCard(DeckId, CardId, Token);

        Assert.IsType<NotFoundResult>(result);
        Assert.False(controller.TempData.ContainsKey(SuccessKey));
    }

    [Fact]
    public async Task Browse_displays_the_deck_with_its_study_cards()
    {
        var deck = AvailableDeck();
        FlashcardDto[] studyCards = [new(CardId, "JOIN", "Combine rows", 6)];
        cards.Setup(service => service.GetStudyCardsAsync(DeckId, UserId, Token)).ReturnsAsync(studyCards);

        var view = Assert.IsType<ViewResult>(await controller.Browse(DeckId, Token));

        Assert.Equal("Browse", view.ViewName);
        var model = Assert.IsType<FlashcardBrowseViewModel>(view.Model);
        Assert.Equal(deck.DeckId, model.DeckId);
        Assert.Equal(deck.Title, model.DeckTitle);
        Assert.Same(studyCards, model.Cards);
    }

    [Fact]
    public async Task Browse_returns_not_found_before_loading_study_cards_for_an_unavailable_deck()
    {
        decks.Setup(service => service.GetDeckDetailAsync(DeckId, UserId, Token))
            .ReturnsAsync((FlashcardDeckDetailDto?)null);

        var result = await controller.Browse(DeckId, Token);

        Assert.IsType<NotFoundResult>(result);
        cards.VerifyNoOtherCalls();
    }

    [Fact]
    public void Study_redirects_to_browse_for_the_same_deck()
    {
        AssertRedirect(controller.Study(DeckId), nameof(FlashcardsController.Browse), DeckId);
    }

    public void Dispose() => cancellation.Dispose();

    private FlashcardDeckDetailDto AvailableDeck()
    {
        var date = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var deck = new FlashcardDeckDetailDto(
            DeckId, UserId, null, "SQL", "Query practice", "Private", date, date, "Databases", true);
        decks.Setup(service => service.GetDeckDetailAsync(DeckId, UserId, Token)).ReturnsAsync(deck);
        return deck;
    }

    private static RedirectToActionResult AssertRedirect(IActionResult result, string action, long? deckId = null)
    {
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(action, redirect.ActionName);
        if (deckId is { } value)
        {
            Assert.NotNull(redirect.RouteValues);
            Assert.Equal(value, redirect.RouteValues["deckId"]);
        }

        return redirect;
    }
}
