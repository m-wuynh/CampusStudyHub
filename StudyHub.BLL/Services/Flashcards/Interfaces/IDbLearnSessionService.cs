using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StudyHub.BLL.DTOs.Flashcards;

namespace StudyHub.BLL.Services.Flashcards.Interfaces
{
    public interface IDbLearnSessionService
    {
        Task<Guid?> StartOrResumeAsync(
            long deckId,
            long userId,
            CancellationToken cancellationToken = default,
            bool ShuffleEnabled = false);

        Task<LearnSessionQuestionDto?> GetOrIssueQuestionAsync(
            Guid sessionId,
            long deckId,
            long userId,
            long cardId,
            CancellationToken cancellationtoken = default);

        Task<FlashcardLearnAnswerResultDto?> SubmitAnswerAsync(
            Guid sessionId,
            long deckId,
            long userId,
            Guid questiontToken,
            SubmitFlashcardLearnAnswerRequestDto request,
            CancellationToken cancellationToken = default);

        Task<DbLearnStateDto?> GetStateAsync(
            Guid sessionId,
            long deckId,
            long userId,
            CancellationToken cancellationtoken = default);

        Task<LearnResultDto?> GetResultAsync(
            Guid sessionId,
            long deckId,
            long userId,
            CancellationToken ct = default);
    }
}
