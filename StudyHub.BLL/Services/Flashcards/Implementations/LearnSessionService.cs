using StudyHub.BLL.DTOs.Flashcards;
using StudyHub.BLL.Services.Flashcards.Interfaces;
using StudyHub.BLL.Services.Flashcards.LearnSessions;
using StudyHub.DAL.Enums.Flashcards;

namespace StudyHub.BLL.Services.Flashcards.Implementations
{
    public class LearnSessionService(
        IFlashcardService flashcardService, ILearnSessionStore sessionStore) : ILearnSessionService
    {
            public async Task<LearnSessionInfoDto?> StartAsync(
                long deckId,
                long currentUserId,
                CancellationToken cancellationToken = default)
            {
                if (deckId <= 0 || currentUserId <= 0)
                {
                    return null;
                }

                //tái sử dụng BLL hiện tại để kiểm tra owner, chỉ lấy các card chưa bị softdelete

                var cards = await flashcardService.GetStudyCardsAsync(
                    deckId,
                    currentUserId,
                    cancellationToken);

                var distinctAnswers = cards.
                    Select(c => c.FrontText.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                if(distinctAnswers.Count() < 2)
                {
                    return null;
                }

                //tránh câu hỏi cùng definition nhưng nhiều term khác nhau
                var eligibleCardIds = cards
                    .GroupBy(c => c.BackText.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Where(group => group.Select(c => c.FrontText.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count() == 1)
                    .SelectMany(group => group)
                    .Select(c => c.Id)
                    .ToList();

                if(eligibleCardIds.Count == 0)
                {
                    return null;
                }

                var session = new LearnSessionState(
                    deckId,
                    currentUserId,
                    eligibleCardIds);

                sessionStore.Save(session);

                return ToInfoDto(session);


            }

            public LearnSessionInfoDto? GetInfo(
                Guid sessionId,
                long deckId,
                long currentUserId)
            {
                var session = sessionStore.Find(
                    sessionId,
                    currentUserId,
                    deckId);

                return session is null ? null : ToInfoDto(session);
            }

            public bool End(
                Guid sessionId,
                long deckId,
                long currentUserId)
            {
                return sessionStore.Remove(
                    sessionId,
                    currentUserId,
                    deckId);
            }

            public async Task<LearnSessionQuestionDto?> GetCurrentQuestionAsync(
                Guid sessionId,
                long deckId,
                long currentUserId,
                CancellationToken cancellationToken = default)
            {
                var session = sessionStore.Find(
                    sessionId,
                    currentUserId,
                    deckId);

                if(session is null)
                {
                    return null;
                }

                var existingQuestion = session.GetIssuedQuestion();
                if(existingQuestion is not null)
                {
                    return existingQuestion;
                }

                var cardId = session.GetOrStartCurrentCard();

                if (!cardId.HasValue)
                {
                    return null;
                }

                var question = await flashcardService.GetLearnQuestionAsync(
                    deckId,
                    currentUserId,
                    questionCardId: cardId.Value,
                    cancellationToken: cancellationToken);

                if (question is null)
                {
                    sessionStore.Remove(
                        sessionId,
                        currentUserId,
                        deckId);

                    return null;
                }

            return session.IssueQuestion(question);

        }

        public async Task<LearnSessionAnswerDto?> AnswerAsync(
            Guid sessionId,
            long deckId,
            long currentUserId,
            Guid questionToken,
            SubmitFlashcardLearnAnswerRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.SelectedAnswer))
            {
                throw new ArgumentException(
                    "Vui lòng chọn một đáp án");
            }

            var session = sessionStore.Find(
                sessionId,
                currentUserId,
                deckId);

            if(session is null)
            {
                return null;
            }

            if(!session.IsValidSelection(
                questionToken,
                request.QuestionCardId,
                request.SelectedAnswer))
            {
                throw new InvalidOperationException(
                    "câu trả lời không thuộc lượt học hiện tại");
            }

            FlashcardLearnAnswerResultDto? gradedAnswer;

            try
            {
                gradedAnswer = await flashcardService.SubmitLearnAnswerAsync(
                    deckId,
                    currentUserId,
                    request,
                    cancellationToken);
            }
            catch (ArgumentException)
            {
                sessionStore.Remove(
                    sessionId,
                    currentUserId,
                    deckId);

                throw new InvalidOperationException(
                    "Dữ liệu deck đã thay đổi. Hãy bắt đầu phiên mới");
            }

            if(gradedAnswer is null)
            {
                sessionStore.Remove(
                    sessionId,
                    currentUserId,
                    deckId);
                return null;
            }

            if (!session.IsValidSelection(
                questionToken,
                request.QuestionCardId,
                gradedAnswer.CorrectAnswer))
            {
                sessionStore.Remove(
                    sessionId,
                    currentUserId,
                    deckId);

                throw new InvalidOperationException(
                    "Dữ liệu deck đã thay đổi. Hãy bắt đầu phiên mới");
            }

            var cardSnapshot = session.ApplyGradedAnswer(
                questionToken,
                request.QuestionCardId,
                request.SelectedAnswer,
                gradedAnswer.IsCorrect);


            return new LearnSessionAnswerDto(
                gradedAnswer,
                cardSnapshot,
                ToInfoDto(session));
        }

            private static LearnSessionInfoDto ToInfoDto(
                LearnSessionState session)
            {
                var cards = session.GetSnapshot();

                return new LearnSessionInfoDto(
                    session.SessionId,
                    session.DeckId,
                    cards.Count,
                    cards.Count(c => c.Status == LearnCardStatus.New),
                    cards.Count(c => c.Status == LearnCardStatus.Learning),
                    cards.Count(c => c.Status == LearnCardStatus.Learned),
                    session.IsCompleted);
            }
        }
    }

