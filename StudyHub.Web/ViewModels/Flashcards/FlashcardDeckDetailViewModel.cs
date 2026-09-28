using StudyHub.BLL.DTOs;
namespace StudyHub.Web.ViewModels.Flashcards
{
    public sealed class FlashcardDeckDetailViewModel
    {
        public required FlashcardDeckDetailDto Deck { get; set; }
        public IReadOnlyList<FlashcardDto> Cards { get; init; } = [];
    }
}
