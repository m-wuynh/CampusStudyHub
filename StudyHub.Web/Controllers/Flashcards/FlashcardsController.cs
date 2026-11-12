using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.Services.Flashcards.Interfaces;
using StudyHub.Web.ViewModels.Flashcards;
using StudyHub.BLL.DTOs.Flashcards;
using StudyHub.BLL.Services.Flashcards;
using Microsoft.AspNetCore.DataProtection.KeyManagement.Internal;
using StudyHub.BLL.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;


namespace StudyHub.Web.Controllers.Flashcards;

[Authorize]
[Route("Flashcards")]
public sealed class FlashcardsController(IFlashcardDeckService flashcardDeckService,
    IFlashcardService flashcardService,
    IDbLearnSessionService dbLearnSessionService,
    ICurrentUser currentUser) : Controller
{

    //deck management

    [AllowAnonymous]
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? searchText,
        CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
            return View(new FlashcardsIndexViewModel { SearchText = searchText });

        var allDecks = await flashcardDeckService.GetMyDecksAsync(
            currentUser.UserId,
            cancellationToken);

        var since = DateTime.UtcNow.AddDays(-7);

        var decks = string.IsNullOrWhiteSpace(searchText)
            ? allDecks
            : allDecks
                .Where(d => d.Title.Contains(
                    searchText.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

        var model = new FlashcardsIndexViewModel
        {
            Decks = decks,
            SearchText = searchText,
            TotalDecks = allDecks.Count,
            NewDecksLast7Days = allDecks.Count(
                d => d.CreatedAtUtc >= since),
            UpdatedDecksLast7Days = allDecks.Count(
                d => d.UpdatedAtUtc >= since)
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
                currentUser.UserId,
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
                currentUser.UserId,
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
            currentUser.UserId,
            cancellationToken);

        if (!deleted)
        {
            return NotFound();
        }

        TempData["FlashcardSuccess"] = "Xóa bộ flashcard thành công";

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
            currentUser.UserId,
            cancellationToken);

        if(deck is null)
        {
            return NotFound();
        }

        var cards = await flashcardService.GetCardsByDeckAsync(
            deckId,
            currentUser.UserId,
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
                currentUser.UserId,
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
                currentUser.UserId,
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
            currentUser.UserId,
            cancellationToken);

        if (!deleted)
        {
            return NotFound();
        }
        TempData["FlashcardSuccess"] = "Xóa thẻ flashcard thành công";

        return RedirectToAction(nameof(Details), new { deckId });
    }

    [HttpGet("{deckId:long}/Browse")]
    public async Task<IActionResult> Browse(
        long deckId,
        CancellationToken cancellationToken)
    {
        var deck = await flashcardDeckService.GetDeckDetailAsync(
            deckId,
            currentUser.UserId,
            cancellationToken);

        if (deck is null)
        {
            return NotFound();
        }

        var cards = await flashcardService.GetStudyCardsAsync(
            deckId,
            currentUser.UserId,
            cancellationToken);

        var model = new FlashcardBrowseViewModel
        {
            DeckId = deck.DeckId,
            DeckTitle = deck.Title,
            Cards = cards
        };

        return View("Browse", model);

    }

    [HttpGet("{deckId:long}/Study")]
    public IActionResult Study(long deckId)
    {
        return RedirectToAction(nameof(Browse), new { deckId });

    }

    //learn mode
    [HttpGet("{deckId:long}/Learn")]
    public async Task<IActionResult> Learn(
        long deckId,
        CancellationToken cancellationToken)
    {
        var deck = await flashcardDeckService.GetDeckDetailAsync(
            deckId,
            currentUser.UserId,
            cancellationToken);

        if(deck is null)
        {
            return NotFound();
        }

        //var sessionId = await dbLearnSessionService.StartOrResumeAsync(
        //    deckId,
        //    currentUser.UserId,
        //    cancellationToken);

        //if (sessionId is null)
        //{
        //    return View("Learn", new FlashcardLearnViewModel
        //    {
        //        DeckId = deck.DeckId,
        //        DeckTitle = deck.Title
        //    });
        //}

        //return RedirectToAction(
        //    nameof(LearnSession),
        //    new { deckId, sessionId = sessionId.Value });
        return View("LearnSetup", new FlashcardLearnViewModel
        {
            DeckId = deck.DeckId,
            DeckTitle = deck.Title
        });

    }


    [HttpPost("{deckId:long}/Learn/Start")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartLearn(
        long deckId,
        bool shuffleEnabled,
        CancellationToken cancellationToken)
    {
        var sessionId = await dbLearnSessionService.StartOrResumeAsync(
            deckId,
            currentUser.UserId,
            cancellationToken,
            shuffleEnabled);

        if (sessionId is null)
        {
            TempData["FlashcardError"] =
                "Không thể bắt đầu phiên học.";

            return RedirectToAction(nameof(Learn), new { deckId });
        }

        return RedirectToAction(nameof(LearnSession),
            new { deckId, sessionId = sessionId.Value });
    }


    [HttpGet("{deckId:long}/Learn/{sessionId:guid}")]
    public async Task<IActionResult> LearnSession(
        long deckId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var deck = await flashcardDeckService.GetDeckDetailAsync(
            deckId,
            currentUser.UserId,
            cancellationToken);

        if(deck is null)
        {
            return NotFound();
        }

        try
        {
            var state = await dbLearnSessionService.GetStateAsync(
                sessionId, deckId, currentUser.UserId, cancellationToken);

            if (state is null)
                return NotFound();

            LearnSessionQuestionDto? question = null;

            if (!state.Session.IsCompleted && state.NextCardId.HasValue)
            {
                question = await dbLearnSessionService.GetOrIssueQuestionAsync(
                    sessionId,
                    deckId,
                    currentUser.UserId,
                    state.NextCardId.Value,
                    cancellationToken);
            }

            LearnResultDto? result = null;
            if (state.Session.IsCompleted)
            {
                result = await dbLearnSessionService.GetResultAsync(
                    sessionId,
                    deckId,
                    currentUser.UserId,
                    cancellationToken);
            }

            return View("Learn", new FlashcardLearnViewModel
            {
                DeckId = deck.DeckId,
                DeckTitle = deck.Title,
                Session = state.Session,
                Question = question,
                Result = result
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("{deckId:long}/Learn/{sessionId:guid}/Answer")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitLearnAnswer(
        long deckId,
        Guid sessionId,
        FlashcardLearnAnswerFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || model.QuestionToken == Guid.Empty)
        {
            return BadRequest(new
            {
                message = "Dữ liệu câu trả lời không hợp lệ"
            });
        }

        try
        {
            var answer = await dbLearnSessionService.SubmitAnswerAsync(
                sessionId,
                deckId,
                currentUser.UserId,
                model.QuestionToken,
                new SubmitFlashcardLearnAnswerRequestDto(
                    model.QuestionCardId,
                    model.SelectedAnswer),
                cancellationToken);

            if (answer is null)
                return NotFound(new { message = "Phiên học không tồn tại." });

            // Submit đã lưu DB; đọc lại tiến độ mới nhất.
            var state = await dbLearnSessionService.GetStateAsync(
                sessionId, deckId, currentUser.UserId, cancellationToken);

            if (state is null)
                return NotFound();

            var progress = state.Session;

            return Json(new
            {
                questionCardId = answer.QuestionCardId,
                selectedAnswer = answer.SelectedAnswer,
                correctAnswer = answer.CorrectAnswer,
                isCorrect = answer.IsCorrect,

                totalCards = progress.TotalCards,
                newCount = progress.NewCount,
                learningCount = progress.LearningCount,
                learnedCount = progress.LearnedCount,
                isCompleted = progress.IsCompleted
            });
        }
        catch(ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch(InvalidOperationException ex)
        {
            return Conflict(new
            {
                message = ex.Message
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                message = "Phiên học đã thay đổi. Hãy tải lại trang."
            });
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException sql &&
                  sql.Number is 2601 or 2627)
        {
            return Conflict(new
            {
                message = "Lượt trả lời đã được ghi nhận. Hãy tải lại trang."
            });
        }
    }

}
