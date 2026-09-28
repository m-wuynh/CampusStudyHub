using StudyHub.BLL.DTOs;
using StudyHub.BLL.Services.Flashcards.Interfaces;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Repositories.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace StudyHub.BLL.Services.Flashcards.Implementations
{
    public sealed class FlashcardDeckService(IRepository repository) : IFlashcardDeckService
    {
        public async Task<IReadOnlyList<FlashcardDeckSummaryDto>> GetMyDecksAsync(
            long currentUserId,
            CancellationToken cancellationToken = default)
        {
            var decks = await repository.ListAsync<FlashcardDeck>(
                deck => deck.OwnerUserId == currentUserId &&
                !deck.IsDeleted, cancellationToken);

            return decks
                .OrderByDescending(deck => deck.UpdatedAtUtc)
                .Select(deck => new FlashcardDeckSummaryDto(
                    deck.DeckId,
                    deck.Title,
                    deck.Description,
                    deck.Visibility,
                    deck.UserSubjectId,
                    deck.CreatedAtUtc,
                    deck.UpdatedAtUtc))
                .ToList();
        }

        public async Task<FlashcardDeckDetailDto?> GetDeckDetailAsync(
            long deckId,
            long currentUserId,
            CancellationToken cancellationToken = default)
        {
            var deck = await repository.FirstOrDefaultAsync<FlashcardDeck>(
                deck =>
                    deck.DeckId == deckId &&
                    deck.OwnerUserId == currentUserId &&
                    !deck.IsDeleted,
                cancellationToken);

            if (deck is null)
            {
                return null;
            }

            return new FlashcardDeckDetailDto(
                deck.DeckId,
                deck.OwnerUserId,
                deck.UserSubjectId,
                deck.StudyGroupId,
                deck.Title,
                deck.Description,
                deck.Visibility,
                deck.CreatedAtUtc,
                deck.UpdatedAtUtc);
        }

        public async Task<long> CreateDeckAsync(
            long currentUserId,
            CreateFlashcardDeckRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var title = request.Title?.Trim();

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException(
                    "Tên deck không được để trống.", nameof(request.Title));
            }

            if(title.Length > 200)
            {
                throw new ArgumentException(
                    "Tên deck không được vượt quá 200 ký tự.", nameof(request.Title));
            }

            var description = request.Description?.Trim();

            if(description?.Length > 2000)
            {
                throw new ArgumentException(
                    "Mô tả deck không được vượt quá 2000 ký tự.", nameof(request.Description));
            }

            await ValidateUserSubjectAsync(
                currentUserId,
                request.UserSubjectId,
                cancellationToken);

            var now = DateTime.UtcNow;

            var deck = new FlashcardDeck
            {
                OwnerUserId = currentUserId,
                UserSubjectId = request.UserSubjectId,

                //chưa làm study group
                StudyGroupId = null,

                Title = title,
                Description = string.IsNullOrWhiteSpace(description)
                ? null : description,

                // ưu tiên Private
                Visibility = "Private",

                IsDeleted = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            await repository.AddAsync(deck, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);

            return deck.DeckId;
        }

        public async Task<bool> UpdateDeckAsync(
            long deckId,
            long currentUserId,
            UpdateFlashcardDeckRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var deck = await repository.FirstOrDefaultAsync<FlashcardDeck>(
                deck =>
                 deck.DeckId == deckId &&
                 deck.OwnerUserId == currentUserId &&
                 !deck.IsDeleted,
                cancellationToken);

            if(deck is null)
            {
                return false;
            }

            var title = request.Title?.Trim();

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException(
                    "Tên bộ flashcard không được để trống", nameof(request.Title));
            }

            if(title.Length > 200)
            {
                throw new ArgumentException(
                    "Tên bộ flashcard không được vượt quá 200 ký tự", nameof(request.Title));
            }

            var desciption = request.Description?.Trim();

            if(desciption?.Length > 2000)
            {
                throw new ArgumentException(
                    "Mô tả không được vượt quá 2000 ký tự", nameof(request.Description));
            }

            await ValidateUserSubjectAsync(
                currentUserId,
                request.UserSubjectId,
                cancellationToken);

            deck.Title = title;
            deck.Description = string.IsNullOrWhiteSpace(desciption) ? null : desciption;

            deck.UserSubjectId = request.UserSubjectId;
            deck.UpdatedAtUtc = DateTime.UtcNow;

            await repository.SaveChangesAsync(cancellationToken);

            return true;
        }

        public async Task<bool> DeleteDeckAsync(
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
                return false;
            }

            deck.IsDeleted = true;
            deck.UpdatedAtUtc = DateTime.UtcNow;

            repository.Update(deck);
            await repository.SaveChangesAsync(cancellationToken);

            return true;
        }

        private async Task ValidateUserSubjectAsync(
            long currentUserId,
            long? userSubjectId,
            CancellationToken cancellationToken)
        {
            if (userSubjectId == null)
            {
                return;
            }

            var belongsToCurrentuser =
                            await repository.AnyAsync<UserSubject>(
                userSubject =>
                    userSubject.UserSubjectId == userSubjectId.Value &&
                    userSubject.UserId == currentUserId &&
                    !userSubject.IsArchived,
                cancellationToken);

            if (!belongsToCurrentuser) { }
            {
                throw new ArgumentException(
                    "Môn học được chọn không thuộc người dùng hiện tại",
                    nameof(userSubjectId));
            }
        }
    }
}
