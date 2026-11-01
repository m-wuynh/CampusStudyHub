using System.ComponentModel.DataAnnotations;
using StudyHub.BLL.DTOs.Flashcards;


namespace StudyHub.Web.ViewModels.Flashcards
{
    public sealed class FlashcardLearnViewModel
    {
        public long DeckId { get; init; }

        public required string DeckTitle { get; init; }
        
        public LearnSessionInfoDto? Session { get; init; }

        public LearnSessionQuestionDto? Question { get; init; }
    }

    public sealed class FlashcardLearnAnswerFormViewModel
    {
        public Guid QuestionToken { get; set; }


        [Range(1, long.MaxValue)]
        public long QuestionCardId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn một đáp án")]
        public string? SelectedAnswer { get; set; }
    }

}
