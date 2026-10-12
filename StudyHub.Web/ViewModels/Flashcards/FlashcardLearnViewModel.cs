using System.ComponentModel.DataAnnotations;
using StudyHub.BLL.DTOs;


namespace StudyHub.Web.ViewModels.Flashcards
{
    public sealed class FlashcardLearnViewModel
    {
        public long DeckId { get; init; }

        public required string DeckTitle { get; init; }

        public FlashcardLearnQuestionDto? Question { get; init; }
    }

    public sealed class FlashcardLearnAnswerFormViewModel
    {
        [Range(1, long.MaxValue)]
        public long QuestionCardId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn một đáp án")]
        public string? SelectedAnswer { get; set; }
    }

}
