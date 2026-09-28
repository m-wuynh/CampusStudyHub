using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StudyHub.BLL.DTOs;

namespace StudyHub.BLL.Services.Flashcards.Interfaces
{
    public interface IFlashcardService
    {
        Task<IReadOnlyList<FlashcardDto>> GetCardsByDeckAsync(
            long deckId,
            long currentUserId,
            CancellationToken cancellationToken = default);

        Task<long> CreateCardAsync(
            long deckId,
            long currentUserId,
            CreateFlashcardRequestDto request,
            CancellationToken cancellationToken = default);

        Task<bool> UpdateCardAsync(
            long cardId,
            long currentUserId,
            UpdateFlashcardRequestDto request,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteCardAsync(
            long cardId,
            long currentUserId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<FlashcardDto>> GetStudyCardsAsync(
            long deckId,
            long currentUserId,
            CancellationToken cancellationToken = default);  

    }
}
