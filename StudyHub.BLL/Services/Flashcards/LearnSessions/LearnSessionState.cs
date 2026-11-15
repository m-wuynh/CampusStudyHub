using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using StudyHub.BLL.DTOs.Flashcards;
using StudyHub.DAL.Enums.Flashcards;

namespace StudyHub.BLL.Services.Flashcards.LearnSessions
{
    public sealed class LearnSessionState
    {
        private readonly object _sync = new();

        private readonly Queue<long> _pendingCards;

        private readonly Dictionary<long, LearnCardState> _cards;

        private long? _currentCardId;

        private FlashcardLearnQuestionDto? _issuedQuestion;
        private Guid _questionToken = Guid.Empty;

        public Guid SessionId { get; } = Guid.NewGuid();

        public long DeckId { get; }

        public long UserId { get; }

        public DateTime StartedAtUtc { get; } = DateTime.UtcNow;

        public LearnSessionState(
            long deckId,
            long userId,
            IEnumerable<long> eligibleCardIds)
        {
            if(deckId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deckId));
            }

            if(userId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(userId));
            }

            ArgumentNullException.ThrowIfNull(eligibleCardIds);

            var cardIds = eligibleCardIds.Distinct().ToList();

            if(cardIds.Count == 0 || cardIds.Any(id => id <= 0))
            {
                throw new ArgumentException(
                    "Session cần ít nhất một card hợp lệ", nameof(eligibleCardIds));
            }

            DeckId = deckId;
            UserId = userId;

            _cards = cardIds.ToDictionary(
                id => id,
                id => new LearnCardState(id));

            _pendingCards = new Queue<long>(cardIds);
        }

        public long? GetOrStartCurrentCard()
        {
            lock (_sync)
            {
                if (_currentCardId.HasValue)
                {
                    return _currentCardId;
                }
                if(_pendingCards.Count == 0)
                {
                    return null;
                }

                _currentCardId = _pendingCards.Dequeue();
                return _currentCardId;
            }
        }

        public LearnSessionQuestionDto? GetIssuedQuestion()
        {
            lock (_sync)
            {
                return _issuedQuestion is null ? null : CreateQuestionDto();
            }
        }

        public LearnSessionQuestionDto IssueQuestion(FlashcardLearnQuestionDto question)
        {
            ArgumentNullException.ThrowIfNull(question);

            lock (_sync)
            {
                if(_currentCardId != question.QuestionCardId)
                {
                    throw new InvalidOperationException(
                        "Câu hỏi không thuộc lượt học hiện ");
                }

                //nếu hau GET chạy đồng thời luôn giữ câu hỏi được phát đầu tiên
                if(_issuedQuestion is not null)
                {
                    return CreateQuestionDto();
                }

                if(question.Options.Count < 2 || question.Options.Count > 4 ||
                    question.Options.Any(o => string.IsNullOrEmpty(o.Text.Trim())) ||
                    question.Options.Select(o => o.Text.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != question.Options.Count)
                {
                    throw new InvalidOperationException(
                        "Các tùy chọn không hợp lệ");
                }

                //sao chép để không giữ collection có thể bị thay đổi từ bên ngoài
                _issuedQuestion = CopyQuestion(question);
                _questionToken = Guid.NewGuid();

                return CreateQuestionDto();
            }
        }

        public bool IsValidSelection(
            Guid questionToken,
            long cardId,
            string selectedAnswer)
        {
            lock (_sync)
            {
                return IsValidSelectionCore(
                    questionToken,
                    cardId,
                    selectedAnswer);
            }
        }

        private bool IsValidSelectionCore(
            Guid questionToken,
            long cardId,
            string selectedAnswer)
        {
            return questionToken != Guid.Empty
                && _questionToken == questionToken
                && _issuedQuestion is not null
                && _currentCardId == cardId
                && _issuedQuestion.QuestionCardId == cardId
                && _issuedQuestion.Options.Any(option =>
                    string.Equals(
                    option.Text.Trim(),
                    selectedAnswer?.Trim(),
                    StringComparison.OrdinalIgnoreCase));
        }

        private LearnSessionQuestionDto CreateQuestionDto()
        {
            return new LearnSessionQuestionDto(
                SessionId,
                _questionToken,
                CopyQuestion(_issuedQuestion!));
        }

        private static FlashcardLearnQuestionDto CopyQuestion(FlashcardLearnQuestionDto question)
        {
            return new FlashcardLearnQuestionDto(
                question.QuestionCardId,
                question.QuestionText,
                question.Options
                    .Select(o => new FlashcardLearnOptionDto(o.Text))
                    .ToList());
        }

        public LearnCardSnapshotDto ApplyGradedAnswer(Guid questionToken,
            long cardId,
            string selectedAnswer,
            bool isCorrect)
        {
            lock (_sync)
            {
                if(!IsValidSelectionCore(
                    questionToken,
                    cardId,
                    selectedAnswer))
                {
                    throw new InvalidOperationException(
                        "Card không phải câu hỏi hiện tại");
                }

                var cardState = _cards[cardId];

                cardState.ApplyGradedAnswer(isCorrect);

                if(cardState.Status != LearnCardStatus.Learned)
                {
                    _pendingCards.Enqueue(cardId);
                }

                _currentCardId = null;
                _issuedQuestion = null;
                _questionToken = Guid.Empty;

                return cardState.ToSnapshot();
            }
        }

        public bool IsCompleted
        {
            get
            {
                lock (_sync)
                {
                    return _currentCardId is null &&
                        _pendingCards.Count == 0;
                }
            }
        }

        public IReadOnlyList<LearnCardSnapshotDto> GetSnapshot()
        {
            lock (_sync)
            {
                return _cards.Values
                    .Select(card => card.ToSnapshot())
                    .ToList();
            }
        }
    }
}
