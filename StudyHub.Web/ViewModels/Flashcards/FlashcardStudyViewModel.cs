using StudyHub.BLL.DTOs;
namespace StudyHub.Web.ViewModels.Flashcards
{
    public sealed class FlashcardStudyViewModel
    {
        public long DeckId { get; init; }
        public required string DeckTitle { get; init; }
        public IReadOnlyList<FlashcardDto> Cards { get; init; } = [];
    }
}
