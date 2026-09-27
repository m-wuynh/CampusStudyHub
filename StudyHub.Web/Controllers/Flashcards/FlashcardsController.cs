using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.Services.Flashcards.Interfaces;
using StudyHub.Web.ViewModels.Flashcards;
using StudyHub.BLL.DTOs;
using StudyHub.BLL.Services.Flashcards;
using Microsoft.AspNetCore.DataProtection.KeyManagement.Internal;


namespace StudyHub.Web.Controllers.Flashcards;

[Route("Flashcards")]
public sealed class FlashcardsController(IFlashcardDeckService flashcardDeckService, IFlashcardService flashcardService) : Controller
{

    //deck management
    private const long DevelopmentUserId = 1;
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? searchText,
        CancellationToken cancellationToken)
    {
        var decks = await flashcardDeckService.GetMyDecksAsync(
            DevelopmentUserId,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var keyword = searchText.Trim();

            decks = decks
                .Where(d => d.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();

        }

        var model = new FlashcardsIndexViewModel
        {
            Decks = decks,
            SearchText = searchText
        };

        return View(model);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        FlashcardDeckFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["FlashcardError"] = "Dữ liệu bộ flashcard không hợp lệ";

            return RedirectToAction(nameof(Index));


        }

        try
        {
            await flashcardDeckService.CreateDeckAsync(
                DevelopmentUserId,
                new CreateFlashcardDeckRequestDto(
                    model.Title,
                    model.Description,
                    model.UserSubjectId),
                cancellationToken
                );

            TempData["FlashcardSuccess"] = "Tạo bộ flashcard thành công";
        }
        catch (ArgumentException ex)
        {
            TempData["FlashcardError"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }


    [HttpPost("{deckId:long}/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        long deckId,
        FlashcardDeckFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["FlashcardError"] = "Dữ liệu bộ flashcard không hợp lệ";

            return RedirectToAction(nameof(Index));
        }

        try
        {
            var updated = await flashcardDeckService.UpdateDeckAsync(
                deckId,
                DevelopmentUserId,
                new UpdateFlashcardDeckRequestDto(
                    model.Title,
                    model.Description,
                    model.UserSubjectId),
                cancellationToken);

            if (!updated)
            {
                return NotFound();
            }

            TempData["FlashcardSuccess"] = "Cập nhật bộ flashcard thành công";
        }
        catch (ArgumentException ex)
        {
            TempData["FlashcardError"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{deckId:long}/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        long deckId,
        CancellationToken cancellationToken)
    {
        var deleted = await flashcardDeckService.DeleteDeckAsync(
            deckId,
            DevelopmentUserId,
            cancellationToken);

        if (!deleted)
        {
            return NotFound();
        }

        TempData["FlashcardScuccess"] = "Xóa bộ flashcard thành công";

        return RedirectToAction(nameof(Index));
    }

    //card management
    [HttpGet("{deckId:long}")]
    public async Task<IActionResult> Details(
        long deckId,
        CancellationToken cancellationToken)
    {
        var deck = await flashcardDeckService.GetDeckDetailAsync(
            deckId,
            DevelopmentUserId,
            cancellationToken);

        if(deck is null)
        {
            return NotFound();
        }

        var cards = await flashcardService.GetCardsByDeckAsync(
            deckId,
            DevelopmentUserId,
            cancellationToken);

        var model = new FlashcardDeckDetailViewModel
        {
            Deck = deck,
            Cards = cards
        };

        return View("Details", model);
    }

    [HttpPost("{deckId:long}/Cards/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCard(
        long deckId,
        FlashcardCardFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["FlashcardError"] = "Dữ liệu thẻ flashcard không hợp lệ";
            return RedirectToAction(nameof(Details), new { deckId });
        }

        try
        {
            await flashcardService.CreateCardAsync(
                deckId,
                DevelopmentUserId,
                new CreateFlashcardRequestDto(
                    model.FrontText,
                    model.BackText,
                    null),
                cancellationToken);

            TempData["FlashcardSuccess"] = "Tạo thẻ flashcard thành công";
        }
        catch(ArgumentException ex)
        {
            TempData["FlashcardError"] = ex.Message;
        }
        catch(InvalidOperationException ex)
        {
            TempData["FlashcardError"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { deckId });
    }

    [HttpPost("{deckId:long}/Cards/{cardId:long}/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCard(
        long deckId,
        long cardId,
        FlashcardCardFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["FlashcardError"] = "Dữ liệu thẻ flashcard không hợp lệ";
            return RedirectToAction(nameof(Details), new { deckId });
        }

        try
        {
            var updated = await flashcardService.UpdateCardAsync(
                cardId,
                DevelopmentUserId,
                new UpdateFlashcardRequestDto(
                    model.FrontText,
                    model.BackText,
                    model.SortOrder),
                cancellationToken);

            if (!updated)
            {
                return NotFound();
            }

            TempData["FlashcardSuccess"] = "Cập nhật thẻ flashcard thành công";
        }
        catch(ArgumentException ex)
        {
            TempData["FlashcardError"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { deckId });
    }

    [HttpPost("{deckId:long}/Cards/{cardId:long}/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCard(
        long deckId,
        long cardId,
        CancellationToken cancellationToken)
    {
        var deleted = await flashcardService.DeleteCardAsync(
            cardId,
            DevelopmentUserId,
            cancellationToken);

        if (!deleted)
        {
            return NotFound();
        }
        TempData["FlashcardSuccess"] = "Xóa thẻ flashcard thành công";

        return RedirectToAction(nameof(Details), new { deckId });
    }

    [HttpGet("{deckId:long}/Study")]
    public async Task<IActionResult> Study(
        long deckId,
        CancellationToken cancellationToken)
    {
        var deck = await flashcardDeckService.GetDeckDetailAsync(
            deckId,
            DevelopmentUserId,
            cancellationToken);

        if (deck is null)
        {
            return NotFound();
        }

        var cards = await flashcardService.GetStudyCardsAsync(
            deckId,
            DevelopmentUserId,
            cancellationToken);

        var model = new FlashcardStudyViewModel
        {
            DeckId = deck.DeckId,
            DeckTitle = deck.Title,
            Cards = cards
        };

        return View("Study", model);

    }

}
