using StudyHub.BLL.DTOs.Flashcards;
namespace StudyHub.Web.ViewModels.Flashcards
{
    public sealed class FlashcardBrowseViewModel
    {
        public long DeckId { get; init; }
        public required string DeckTitle { get; init; }
        public IReadOnlyList<FlashcardDto> Cards { get; init; } = [];
    }
}
