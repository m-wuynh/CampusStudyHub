using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StudyHub.BLL.DTOs.Flashcards;
using StudyHub.BLL.Services.Flashcards.LearnSessions;

namespace StudyHub.BLL.Services.Flashcards.Interfaces
{
    public interface ILearnSessionService
    {
        Task<LearnSessionInfoDto?> StartAsync(
            long deckId,
            long currentUserId,
            CancellationToken cancellationToken = default);

        LearnSessionInfoDto? GetInfo(
            Guid sessionId,
            long deckId,
            long currentUserId);

        bool End(
            Guid sessionId,
            long deckId,
            long currentUserId);

        Task<LearnSessionQuestionDto?> GetCurrentQuestionAsync(
            Guid sessionId,
            long deckId,
            long currentId,
            CancellationToken cancellationToken = default);

        Task<LearnSessionAnswerDto?> AnswerAsync(
            Guid sessionId,
            long deckId,
            long currentUserId,
            Guid questionToken,
            SubmitFlashcardLearnAnswerRequestDto request,
            CancellationToken cancellationToken = default);

    }
}
