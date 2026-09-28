using System.ComponentModel.DataAnnotations;

namespace StudyHub.Web.ViewModels.Flashcards
{
    public sealed class FlashcardCardFormViewModel
    {
        public long? FlashcardId { get; set; }

        public long DeckId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mặt trước của thẻ học.")]
        [StringLength(4000, ErrorMessage = "Mặt trước của thẻ học không được vượt quá 4000 ký tự.")]
        public string? FrontText { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mặt sau của thẻ học.")]
        [StringLength(4000, ErrorMessage = "Mặt sau của thẻ học không được vượt quá 4000 ký tự.")]
        public string? BackText { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Thứ tự thẻ học không được nhỏ hơn 0.")]
        public int SortOrder { get; set; }
    }
}
