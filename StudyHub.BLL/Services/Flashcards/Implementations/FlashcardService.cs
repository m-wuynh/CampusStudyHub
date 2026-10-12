using Microsoft.Identity.Client;
using StudyHub.BLL.DTOs;
using StudyHub.BLL.Services.Flashcards.Interfaces;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Repositories.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyHub.BLL.Services.Flashcards.Implementations
{
    public sealed class FlashcardService(IRepository repository) : IFlashcardService
    {
        public async Task<IReadOnlyList<FlashcardDto>> GetCardsByDeckAsync(
            long deckId,
            long currentUserId,
            CancellationToken cancellationToken = default)
        {
            var deck = await repository.FirstOrDefaultAsync<FlashcardDeck>(
                d => d.DeckId == deckId &&
                d.OwnerUserId == currentUserId &&
                !d.IsDeleted,
                cancellationToken);

            if(deck is null)
            {
                return [];
            }

            var cards = await repository.ListAsync<Flashcard>(
                c => c.DeckId == deckId &&
                !c.IsDeleted,
                cancellationToken);

            return cards
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.FlashcardId)
                .Select(c => new FlashcardDto(
                    c.FlashcardId,
                    c.FrontText,
                    c.BackText,
                    c.SortOrder))
                .ToList();
        }

        public async Task<long> CreateCardAsync(
            long deckId,
            long currentUserId,
            CreateFlashcardRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var deck = await repository.FirstOrDefaultAsync<FlashcardDeck>(
                d => d.DeckId == deckId &&
                d.OwnerUserId == currentUserId &&
                !d.IsDeleted,
                cancellationToken);

            if(deck is null)
            {
                throw new InvalidOperationException(
                    "Deck không tồn tại hoặc không có quyền chỉnh sửa");
            }

            var frontText = request.FrontText?.Trim();
            var backText = request.BackText?.Trim();

            ValidateCardText(frontText, backText);

            if(frontText!.Length > 4000)
            {
                throw new ArgumentException(
                    "Mặt trước không được vượt quá 4000 ký tự");
            }

            if(backText!.Length > 4000)
            {
                throw new ArgumentException(
                    "Mặt sau không được vượt quá 4000 ký tự");
            }

            int sortOrder;

            if (request.SortOrder.HasValue)
            {
                if(request.SortOrder.Value < 0)
                {
                    throw new ArgumentException(
                        "Thứ tự thẻ không được nhỏ hơn 0");
                }

                sortOrder = request.SortOrder.Value;
            }
            else
            {
                var exisitingCards = await repository.ListAsync<Flashcard>(
                    c => c.DeckId == deckId &&
                    !c.IsDeleted,
                    cancellationToken);

                sortOrder = exisitingCards.Count == 0 ? 0 : exisitingCards.Max(c => c.SortOrder) + 1;
            }

            var now = DateTime.UtcNow;
            var card = new Flashcard
            {
                DeckId = deckId,
                FrontText = frontText,
                BackText = backText,
                SortOrder = sortOrder,
                IsDeleted = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            await repository.AddAsync(card, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
            return card.FlashcardId;
        }

        public async Task<bool> UpdateCardAsync(
            long cardId,
            long currentuserID,
            UpdateFlashcardRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var card = await repository.FirstOrDefaultAsync<Flashcard>(
                c => c.FlashcardId == cardId &&
                !c.IsDeleted,
                cancellationToken);

            if (card is null)
            {
                return false;
            }

            var deck = await repository.FirstOrDefaultAsync<FlashcardDeck>(
               d => d.DeckId == card.DeckId &&
               d.OwnerUserId == currentuserID &&
               !d.IsDeleted,
               cancellationToken);

            if (deck is null)
            {
                return false;
            }

            var frontText = request.FrontText?.Trim();
            var backText = request.BackText?.Trim();

            ValidateCardText(frontText, backText);

            if(frontText!.Length > 4000 || backText!.Length > 4000)
            {
                throw new ArgumentException(
                    "Mặt trước hoặc mặt sau không được vượt quá 4000 ký tự");
            }

            if(request.SortOrder < 0)
            {
                throw new ArgumentException(
                    "Thứ tự thẻ không được nhỏ hơn 0");
            }
            card.FrontText = frontText;
            card.BackText = backText;
            card.SortOrder = request.SortOrder;
            card.UpdatedAtUtc = DateTime.UtcNow;

            repository.Update(card);
            await repository.SaveChangesAsync(cancellationToken);

            return true;
        }

        public async Task<bool> DeleteCardAsync(
            long cardId,
            long currentUserUd,
            CancellationToken cancellationToken = default)
        {
            var card = await repository.FirstOrDefaultAsync<Flashcard>(
                c => c.FlashcardId == cardId &&
                !c.IsDeleted,
                cancellationToken);

            if(card is null)
            {
                return false;
            }

            var deck = await repository.FirstOrDefaultAsync<FlashcardDeck>(
                d => d.DeckId == card.DeckId &&
                d.OwnerUserId == currentUserUd &&
                !d.IsDeleted,
                cancellationToken);

            if(deck is null)
            {
                return false;
            }

            card.IsDeleted = true;
            card.UpdatedAtUtc = DateTime.UtcNow;

            repository.Update(card);
            await repository.SaveChangesAsync(cancellationToken);

            return true;
        }

        public async Task<IReadOnlyList<FlashcardDto>> GetStudyCardsAsync(
            long deckId,
            long currentUserId,
            CancellationToken cancellationToken = default)
        {
            var deck = await repository.FirstOrDefaultAsync<FlashcardDeck>(
                d => d.DeckId == deckId &&
                d.OwnerUserId == currentUserId &&
                !d.IsDeleted,
                cancellationToken);

            if(deck is null)
            {
                return [];
            }

            var cards = await repository.ListAsync<Flashcard>(
                c => c.DeckId == deckId &&
                !c.IsDeleted,
                cancellationToken);

            return cards
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.FlashcardId)
                .Select(c => new FlashcardDto(
                    c.FlashcardId,
                    c.FrontText,
                    c.BackText,
                    c.SortOrder))
                .ToList();
        }

        public async Task<FlashcardLearnQuestionDto?> GetLearnQuestionAsync(
            long deckId,
            long currentUserId,
            long? questionCardId = null,
            CancellationToken cancellationToken = default)
        {
            var deck = await repository.FirstOrDefaultAsync<FlashcardDeck>(
                d => d.DeckId == deckId &&
                d.OwnerUserId == currentUserId &&
                !d.IsDeleted,
                cancellationToken);

            if (deck is null)
            {
                return null;
            }

            var cards = await repository.ListAsync<Flashcard>(
                c => c.DeckId == deckId &&
                !c.IsDeleted,
                cancellationToken);

            if (cards.Count == 0)
            {
                return null;
            }

            //learn mode cần tối thiểu ít nhất 2 đáp án fronttext khác nhau
            var distinctFrontTexts = cards.
                Select(c => c.FrontText.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (distinctFrontTexts.Count < 2)
            {
                return null;
            }

            Flashcard? questionCard;

            if (questionCardId.HasValue)
            {
                questionCard = cards.FirstOrDefault(
                    c => c.FlashcardId == questionCardId.Value);

                if (questionCard is null)
                {
                    return null;
                }
            }
            else
            {
                var eligibleCards = cards.GroupBy(
                    card => card.BackText.Trim(),
                    StringComparer.OrdinalIgnoreCase)
                    .Where(group => group
                        .Select(card => card.FrontText.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count() == 1)
                    .SelectMany(group => group)
                    .ToList();

                if (eligibleCards.Count == 0)
                {
                    return null;
                }

                questionCard = eligibleCards[Random.Shared.Next(eligibleCards.Count)];
            }

                if (!IsLearnQuestionUnamibiguous(questionCard, cards))
                {
                    return null;
                }

                var correctAnswer = questionCard.FrontText.Trim();

                var distractors = cards
                    .Where(c => c.FlashcardId != questionCard.FlashcardId)
                    .Select(c => c.FrontText.Trim())
                    .Where(text =>
                    !string.Equals(
                        text,
                        correctAnswer,
                        StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                Shuffle(distractors);

                var optionTexts = distractors
                    .Take(3)
                    .Append(correctAnswer)
                    .ToList();

                Shuffle(optionTexts);

                var options = optionTexts
                    .Select(text => new FlashcardLearnOptionDto(text))
                    .ToList();

                return new FlashcardLearnQuestionDto(
                    questionCard.FlashcardId,
                    questionCard.BackText,
                    options);
            
        }

        public async Task<FlashcardLearnAnswerResultDto?> SubmitLearnAnswerAsync(
            long deckId,
            long currentUserId,
            SubmitFlashcardLearnAnswerRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var selectedAnswer = request.SelectedAnswer?.Trim();

            if (string.IsNullOrWhiteSpace(selectedAnswer))
            {
                throw new ArgumentException(
                    "Vui lòng chọn một đáp án",
                    nameof(request.SelectedAnswer));
            }

            var deck = await repository.FirstOrDefaultAsync<FlashcardDeck>(
                d => d.DeckId == deckId &&
                    d.OwnerUserId == currentUserId &&
                    !d.IsDeleted,
                cancellationToken);

            if(deck is null)
            {
                return null;
            }

            var cards = await repository.ListAsync<Flashcard>(
                c => c.DeckId == deckId &&
                !c.IsDeleted,
                cancellationToken);

            if(cards.Count == 0)
            {
                return null;
            }

            var questionCard = cards.FirstOrDefault(
                c => c.FlashcardId == request.QuestionCardId);

            if(questionCard is null)
            {
                return null;
            }

            if(!IsLearnQuestionUnamibiguous(questionCard, cards))
            {
                return null;
            }

            var distinctFrontTexts = cards
                .Select(c => c.FrontText.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if(distinctFrontTexts.Count < 2)
            {
                return null;
            }

            //selectedanswer  phải là một đáp án đúng tồn tại trong deck
            var answerExistsInDeck = distinctFrontTexts.Any(
                answer => string.Equals(
                    answer,
                    selectedAnswer,
                    StringComparison.OrdinalIgnoreCase));

            if (!answerExistsInDeck)
            {
                throw new ArgumentException(
                    "Đáp án được chọn không tồn tại trong bộ thẻ",
                    nameof(request.SelectedAnswer));
            }

            var correctAnswer = questionCard.FrontText.Trim();

            var isCorrect = string.Equals(
                selectedAnswer,
                correctAnswer,
                StringComparison.OrdinalIgnoreCase);

            return new FlashcardLearnAnswerResultDto(
                questionCard.FlashcardId,
                selectedAnswer,
                correctAnswer,
                isCorrect);
        }

        private static bool IsLearnQuestionUnamibiguous(
            Flashcard questionCard,
            IReadOnlyCollection<Flashcard> cards)
        {
            var questionText = questionCard.BackText.Trim();
            var correctAnswer = questionCard.FrontText.Trim();

            return !cards.Any(other =>
            other.FlashcardId != questionCard.FlashcardId &&
            string.Equals(
                other.BackText.Trim(),
                questionText,
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                other.FrontText.Trim(),
                correctAnswer,
                StringComparison.OrdinalIgnoreCase));
        }

        private static void Shuffle<T>(IList<T> items)
        {
            for(var i = items.Count - 1; i > 0; i--)
            {
                var j = Random.Shared.Next(i + 1);

                (items[i], items[j]) = (items[j], items[i]);  
            }
        }


        private static void ValidateCardText(
            string? frontText,
            string? backText)
        {
            if (string.IsNullOrWhiteSpace(frontText))
            {
                throw new ArgumentException("Mặt trước flashcard không được để trống");
            }

            if(string.IsNullOrWhiteSpace(backText)) 
            {
                throw new ArgumentException("Mặt sau flashcard không được để trống");
            }
        }
    }
}
