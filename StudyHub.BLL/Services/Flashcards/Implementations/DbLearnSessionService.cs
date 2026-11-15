using Microsoft.EntityFrameworkCore.Query;
using Microsoft.VisualBasic;
using StudyHub.BLL.DTOs.Flashcards;
using StudyHub.BLL.Services.Flashcards.Interfaces;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Enums.Flashcards;
using StudyHub.DAL.Repositories.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace StudyHub.BLL.Services.Flashcards.Implementations
{
    public sealed class DbLearnSessionService(IRepository repository, IFlashcardService flashcardService) : IDbLearnSessionService
    {
        public async Task<Guid?> StartOrResumeAsync(
            long deckId,
            long userId,
            CancellationToken cancellationToken = default,
            bool ShuffleEnabled = false)
        {
            if(deckId <= 0 || userId <= 0)
            {
                return null;
            }

            //kiểm tra quyền owner
            var deck = await repository.FirstOrDefaultAsync<FlashcardDeck>(
                d => d.DeckId == deckId &&
                      d.OwnerUserId == userId &&
                      !d.IsDeleted, cancellationToken);

            if (deck is null)
            {
                return null;
            }

            //ưu tiên khôi phục session chưa hoàn thành
            var existing = repository.Query<FlashcardLearnSession>()
                .Where(s => s.UserId == userId &&
                            s.DeckId == deckId &&
                            s.CompletedAtUtc == null &&
                            s.InvalidatedAtUtc == null)
                .OrderByDescending(s => s.StartedAtUtc)
                .FirstOrDefault();

            if (existing is not null)
            {
                if (!await HasCardsChangedAsync(existing, cancellationToken))
                    return existing.SessionId;

                existing.InvalidatedAtUtc = DateTime.UtcNow;
                existing.CurrentCardId = null;
                existing.QuestionToken = null;
                existing.QuestionOptionsJson = null;

                repository.Update(existing);
                await repository.SaveChangesAsync(cancellationToken);
            }

            //tái sử dụng cách kiểm tra card cũ
            var cards = await flashcardService.GetStudyCardsAsync(
                deckId, userId, cancellationToken);

            var distinctAnswers = cards
                .Select(c => c.FrontText.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            if (distinctAnswers < 2)
                return null;

            var eligilbleCards = cards
                .GroupBy(c => c.BackText.Trim(),
                        StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Select(c => c.FrontText.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count() == 1)
                .SelectMany(g => g)
                .ToList();

            if (eligilbleCards.Count == 0)
                return null;

            var session = new FlashcardLearnSession
            {
                SessionId = Guid.NewGuid(),
                UserId = userId,
                DeckId = deckId,
                StartedAtUtc = DateTime.UtcNow,
                ShuffleEnabled = ShuffleEnabled
            };

            await repository.AddAsync(session, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);

            return session.SessionId;




        }

        public async Task<LearnSessionQuestionDto?> GetOrIssueQuestionAsync(
            Guid sessionId,
            long deckId,
            long userId,
            long cardId,
            CancellationToken cancellationtoken = default)
        {
            //dùng FindAsync lấy entity có tracking để cập nhật db
            var session = await repository.FindAsync<FlashcardLearnSession>(
                new object[] { sessionId }, cancellationtoken);

            if (session is null ||
                session.UserId != userId ||
                session.DeckId != deckId ||
                session.CompletedAtUtc != null ||
                session.InvalidatedAtUtc != null)
                return null;

            var state = await GetStateAsync(
                sessionId, deckId, userId, cancellationtoken);

            if (state is null || state.Session.IsCompleted)
                return null;

            if (state.NextCardId != cardId)
                throw new InvalidOperationException(
                    "Card không thuộc lượt học hiện tại.");

            //đảm bảo deck vẫn thuộc owner
            var cards = await flashcardService.GetStudyCardsAsync(
                deckId, userId, cancellationtoken);

            if (cards.Count == 0)
                return null;
            //nếu có câu hỏi thì khôi phục từ db
            if (session.CurrentCardId.HasValue)
            {
                if (!session.QuestionToken.HasValue ||
                   string.IsNullOrWhiteSpace(session.QuestionOptionsJson))
                    throw new InvalidOperationException("Trạng thái câu hỏi không hợp lệ");
                var currentCard = cards.FirstOrDefault(
                    c => c.Id == session.CurrentCardId.Value);

                if (currentCard is null)
                    throw new InvalidOperationException("Deck đã thay đổi. Hãy bắt đầu phiên mới");

                var savedOptions = JsonSerializer.Deserialize<
                    List<FlashcardLearnOptionDto>>(
                       session.QuestionOptionsJson);

                if(savedOptions is null)
                    throw new InvalidOperationException(
                        "Không thể khôi phục đáp án");

                return new LearnSessionQuestionDto(
                    sessionId,
                    session.QuestionToken.Value,
                    new FlashcardLearnQuestionDto(
                        currentCard.Id,
                        currentCard.BackText,
                        savedOptions));
            }
            //chỉ phát câu hỏi mới khi không có câu đang chờ Submit
            var question = await flashcardService.GetLearnQuestionAsync(
                deckId, userId, cardId, cancellationtoken);

            if (question is null)
                return null;

            session.CurrentCardId = question.QuestionCardId;
            session.QuestionToken = Guid.NewGuid();
            session.QuestionOptionsJson = JsonSerializer.Serialize(question.Options);

            await repository.SaveChangesAsync(cancellationtoken);

            return new LearnSessionQuestionDto(
                sessionId,
                session.QuestionToken.Value,
                question);
        }

        public async Task<FlashcardLearnAnswerResultDto?> SubmitAnswerAsync(
            Guid sessionId,
            long deckId,
            long userId,
            Guid questionToken,
            SubmitFlashcardLearnAnswerRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            await using var transaction = await repository.BeginTransactionAsync(cancellationToken);

            //kiểm tra phiên và câu hỏi đang chờ
            var session = await repository.FindAsync<FlashcardLearnSession>(
                new object[] { sessionId }, cancellationToken
                );

            if (session is null ||
                session.UserId != userId ||
                session.DeckId != deckId ||
                session.CompletedAtUtc != null ||
                session.InvalidatedAtUtc != null)
                return null;

            if (questionToken == Guid.Empty ||
                session.QuestionToken != questionToken ||
                session.CurrentCardId != request.QuestionCardId)
                throw new InvalidOperationException("Câu hỏi đã hết hiệu lực");

            //kiểm tra đáp án đã được phát
            if (string.IsNullOrWhiteSpace(session.QuestionOptionsJson))
                throw new InvalidOperationException("Câu hỏi chưa được phát");

            var options = JsonSerializer.Deserialize<List<FlashcardLearnOptionDto>>(
                session.QuestionOptionsJson);

            var selectedAnswer = request.SelectedAnswer?.Trim();

            if (options is null ||
                string.IsNullOrWhiteSpace(selectedAnswer) ||
                !options.Any(o => string.Equals(
                    o.Text, selectedAnswer,
                    StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Đáp án không hợp lệ");


            //chấm đáp án bằng bll hiẹn tại
            var answer = await flashcardService.SubmitLearnAnswerAsync(
                deckId, userId, request, cancellationToken);

            if (answer is null ||
                !options.Any(o => string.Equals(
                    o.Text, answer.CorrectAnswer,
                    StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Deck đã thay đổi. Hãy bắt đầu phiên mới");

            var now = DateTime.UtcNow;

            var rating = answer.IsCorrect
                ? FlashcardReviewRating.Good
                : FlashcardReviewRating.Again;

            //đảm bảo Progress tồn tại để thỏa khóa ngoại
            var progress = await repository.FindAsync<FlashcardProgress>(
                new object[] { userId, request.QuestionCardId }, cancellationToken);

            if (progress is null)
            {
                progress = new FlashcardProgress
                {
                    UserId = userId,
                    FlashcardId = request.QuestionCardId,
                    NextReviewAtUtc = now,
                    EaseFactor = 2.50m
                };

                await repository.AddAsync(progress, cancellationToken);
            }

            //ghi lịch sử trả lời
            await repository.AddAsync(new FlashcardReview
            {
                RequestId = questionToken,
                SessionId = sessionId,
                UserId = userId,
                FlashcardId = request.QuestionCardId,
                Rating = (byte)rating,
                ReviewedAtUtc = now,
                FlashcardProgress = progress
            }, cancellationToken);

            //kết thúc lượt câu hỏi đang chờ
            session.CurrentCardId = null;
            session.QuestionToken = null;
            session.QuestionOptionsJson = null;

            await repository.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return answer;

        }


        public async Task<DbLearnStateDto?> GetStateAsync(
            Guid sessionId,
            long deckId,
            long userId,
            CancellationToken cancellationtoken = default)
        {
            var session = await repository.FindAsync<FlashcardLearnSession>(
                new object[] { sessionId }, cancellationtoken);

            if (session is null ||
                session.UserId != userId ||
                session.DeckId != deckId)
                return null;

            if (session.InvalidatedAtUtc.HasValue)
                throw new InvalidOperationException(
                    "Phiên học đã bị hủy do Deck thay đổi. Hãy bắt đầu lại.");

            if (session.CompletedAtUtc is null && 
                await HasCardsChangedAsync(session, cancellationtoken))
                throw new InvalidOperationException(
                    "Deck đã thay đổi. Hãy bắt đầu phiên mới");

            //chỉ owner được learn
            var cards = await flashcardService.GetStudyCardsAsync(
                deckId, userId, cancellationtoken);

            if(cards.Select(c => c.FrontText.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() < 2)
                throw new InvalidOperationException("Deck không đủ câu hỏi để học");

            //quy tắc lọc Card
            var eligible = cards
                .GroupBy(c => c.BackText.Trim(),
                    StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Select(c => c.FrontText.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1)
                .SelectMany(g => g)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Id)
                .ToList();

            if(eligible.Count == 0)
                throw new InvalidOperationException("Khong còn Card hợp lệ trong Deck");

            //lấy lịch sử trả lời của phiên hiện tại
            var reviews = await repository.ListAsync<FlashcardReview>(
                r => r.SessionId == sessionId &&
                r.UserId == userId, cancellationtoken);

            var states = eligible.Select(card =>
            {
                var history = reviews
                      .Where(r => r.FlashcardId == card.Id)
                      .OrderBy(r => r.ReviewedAtUtc)
                      .ThenBy(r => r.ReviewId)
                      .ToList();

                int correct = history.Count(r =>
                    r.Rating == (byte)FlashcardReviewRating.Good);

                int incorrect = history.Count(r =>
                    r.Rating == (byte)FlashcardReviewRating.Again);

                var status = history.Count == 0
                            ? LearnCardStatus.New
                            : correct >= 2
                            ? LearnCardStatus.Learned
                            : LearnCardStatus.Learning;

                return new
                {
                    CardId = card.Id,
                    card.SortOrder,
                    Status = status,
                    LastReview = history.LastOrDefault()?.ReviewedAtUtc
                };

            }).ToList();

            int newCount = states.Count(s => s.Status == LearnCardStatus.New);
            int learningCount = states.Count(s => s.Status == LearnCardStatus.Learning);
            int learnedCount = states.Count(s => s.Status == LearnCardStatus.Learned);

            bool completed = learnedCount == states.Count;

            if(completed && session.CompletedAtUtc is null)
            {
                session.CompletedAtUtc = DateTime.UtcNow;
                await repository.SaveChangesAsync(cancellationtoken);
            }

            //ưu tiên Card mới sau đó ôn lại card đang Learning
            //long? nextCardId = completed ? null :
            //    session.CurrentCardId ??
            //    states.Where(s => s.Status != LearnCardStatus.Learned)
            //        .OrderBy(s => s.Status == LearnCardStatus.New ? 0 : 1)
            //        .ThenBy(s => s.LastReview ?? DateTime.MinValue)
            //        .ThenBy(s => s.SortOrder)
            //        .Select(s => (long?)s.CardId)
            //        .FirstOrDefault();


            // Số lượt đã trả lời của từng Card trong Session
            var attempts = reviews
                .GroupBy(r => r.FlashcardId)
                .ToDictionary(g => g.Key, g => g.Count());

            // Chọn Card chưa Learned, theo thứ tự cố định của Session
            var candidates = states
                .Where(s => s.Status != LearnCardStatus.Learned)
                .OrderBy(s => attempts.GetValueOrDefault(s.CardId));

            var ordered = session.ShuffleEnabled
                ? candidates
                    .ThenBy(s => ShuffleKey(session.SessionId, s.CardId),
                        StringComparer.Ordinal)
                : candidates
                    .ThenBy(s => s.SortOrder)
                    .ThenBy(s => s.CardId);

            long? nextCardId = completed
                ? null
                : session.CurrentCardId ??
                  ordered.Select(s => (long?)s.CardId).FirstOrDefault();


            var info = new LearnSessionInfoDto(
                sessionId,
                deckId,
                states.Count,
                newCount,
                learningCount,
                learnedCount,
                completed,
                session.ShuffleEnabled);

            return new DbLearnStateDto(info, nextCardId);

        }

        public async Task<LearnResultDto?> GetResultAsync(
            Guid sessionId,
            long deckId,
            long userId,
            CancellationToken cancellationtoken = default)
        {
            var state = await GetStateAsync(
                sessionId, deckId, userId, cancellationtoken);

            if (state is null)
                return null;

            var reviews = await repository.ListAsync<FlashcardReview>(
                r => r.SessionId == sessionId &&
                r.UserId == userId, cancellationtoken);

            int correct = reviews.Count(r =>
                r.Rating == (byte)FlashcardReviewRating.Good);

            int incorrect = reviews.Count(r =>
                r.Rating == (byte)FlashcardReviewRating.Again);

            int total = correct + incorrect;

            var progress = state.Session;

            return new LearnResultDto(
                sessionId,
                deckId,
                total,
                correct,
                incorrect,
                total == 0 ? 0 : Math.Round(100.0 * correct / total, 2),
                progress.TotalCards,
                progress.LearnedCount,
                progress.TotalCards - progress.LearnedCount,
                progress.IsCompleted);
        }

        //helper kiểm tra xem card đã thay đổi chưa
        private Task<bool> HasCardsChangedAsync(
            FlashcardLearnSession session,
            CancellationToken cancellationToken)
        {
            return repository.AnyAsync<Flashcard>(
                c => c.DeckId == session.DeckId &&
                (c.CreatedAtUtc > session.StartedAtUtc ||
                c.UpdatedAtUtc > session.StartedAtUtc),
                cancellationToken);
        }

        
        private static string ShuffleKey(Guid sessionId, long cardId)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(
                $"{sessionId:N}:{cardId}");

            return Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(bytes));
        }

        //helper shuffle card khi Learn

    }
}
