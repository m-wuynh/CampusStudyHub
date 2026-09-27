using System.ComponentModel.DataAnnotations;
using StudyHub.BLL.DTOs;

namespace StudyHub.Web.ViewModels.Flashcards;
public sealed class FlashcardsIndexViewModel
{
    public IReadOnlyList<FlashcardDeckSummaryDto> Decks { get; init; } = [];

    public string? SearchText { get; set; }

    public long? UserSubjectId { get; init; }

    public FlashcardDeckFormViewModel CreateDeck { get; init; } = new();
}

public sealed class FlashcardDeckFormViewModel
{
    public long? DeckId { get; set; }

    [Required(ErrorMessage = "Tên bộ thẻ là bắt buộc")]
    [StringLength(200, ErrorMessage = "Tên bộ thẻ không được vượt quá 200 ký tự")]
    public string? Title { get; set; }

    [StringLength(2000, ErrorMessage = "Mô tả không được vượt quá 2000 ký tự")]
    public string? Description { get; set; }

    public long? UserSubjectId { get; set; }
}

