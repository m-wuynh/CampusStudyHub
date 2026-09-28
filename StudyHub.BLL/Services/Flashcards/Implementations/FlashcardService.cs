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
