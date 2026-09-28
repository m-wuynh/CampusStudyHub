using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StudyHub.BLL.DTOs;

namespace StudyHub.BLL.Services.Flashcards.Interfaces
{
    public interface IFlashcardDeckService
    {
        Task<IReadOnlyList<FlashcardDeckSummaryDto>> GetMyDecksAsync(
            long currentUserId,
            CancellationToken cancellationToken = default);

        Task<FlashcardDeckDetailDto?> GetDeckDetailAsync(
            long deckId,
            long currentUserId,
            CancellationToken cancellationToken = default);

        Task<long> CreateDeckAsync(
            long currentUserId,
            CreateFlashcardDeckRequestDto request,
            CancellationToken cancellationToken = default);

        Task<bool> UpdateDeckAsync(
            long deckId,
            long currentUserId,
            UpdateFlashcardDeckRequestDto request,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteDeckAsync(
            long deckId,
            long currentUserId,
            CancellationToken cancellationToken = default);
    }
}
