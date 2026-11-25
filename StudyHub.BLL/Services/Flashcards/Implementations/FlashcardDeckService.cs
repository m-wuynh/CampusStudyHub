using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs.Flashcards;
using StudyHub.BLL.Services.Flashcards.Interfaces;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Repositories.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
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
            //var decks = await repository.ListAsync<FlashcardDeck>(
            //    deck => deck.OwnerUserId == currentUserId &&
            //    !deck.IsDeleted, cancellationToken);

            //return decks
            //    .OrderByDescending(deck => deck.UpdatedAtUtc)
            //    .Select(deck => new FlashcardDeckSummaryDto(
            //        deck.DeckId,
            //        deck.Title,
            //        deck.Description,
            //        deck.Visibility,
            //        deck.CreatedAtUtc,
            //        deck.UpdatedAtUtc,
            //        deck.UserSubjectId,
            //        deck.UserSubject == null ? null : deck.UserSubject.CustomSubjectName ?? deck.UserSubject.Subject!.SubjectName))
            //    .ToList();

            return await repository.Query<FlashcardDeck>()
                .Where(deck => deck.OwnerUserId == currentUserId && !deck.IsDeleted)
                .OrderByDescending(deck => deck.UpdatedAtUtc)
                .Select(deck => new FlashcardDeckSummaryDto(
                    deck.DeckId,
                    deck.Title,
                    deck.Description,
                    deck.Visibility,
                    deck.CreatedAtUtc,
                    deck.UpdatedAtUtc,
                    deck.UserSubjectId,
                    deck.UserSubject == null ? null
                    : deck.UserSubject.CustomSubjectName ?? deck.UserSubject.Subject!.SubjectName))
                .ToListAsync(cancellationToken);
        }

        //public async Task<FlashcardDeckDetailDto?> GetDeckDetailAsync(
        //    long deckId,
        //    long currentUserId,
        //    CancellationToken cancellationToken = default)
        //{
        //    //var deck = await repository.FirstOrDefaultAsync<FlashcardDeck>(
        //    //    deck =>
        //    //        deck.DeckId == deckId &&
        //    //        deck.OwnerUserId == currentUserId &&
        //    //        !deck.IsDeleted,
        //    //    cancellationToken);

        //    //if (deck is null)
        //    //{
        //    //    return null;
        //    //}

        //    //return new FlashcardDeckDetailDto(
        //    //    deck.DeckId,
        //    //    deck.OwnerUserId,
        //    //    deck.StudyGroupId,
        //    //    deck.Title,
        //    //    deck.Description,
        //    //    deck.Visibility,
        //    //    deck.CreatedAtUtc,
        //    //    deck.UpdatedAtUtc);


        //}

        public async Task<FlashcardDeckDetailDto?> GetDeckDetailAsync(
            long deckId,
            long currentUserId,
            CancellationToken cancellationToken = default)
        {
            return await repository.Query<FlashcardDeck>()
                .Where(deck => deck.DeckId == deckId &&
                               deck.OwnerUserId == currentUserId &&
                               !deck.IsDeleted)
                .Select(deck => new FlashcardDeckDetailDto(
                    deck.DeckId,
                    deck.OwnerUserId,
                    deck.StudyGroupId,
                    deck.Title,
                    deck.Description,
                    deck.Visibility,
                    deck.CreatedAtUtc,
                    deck.UpdatedAtUtc,
                    deck.UserSubject == null
                        ? null
                        : deck.UserSubject.CustomSubjectName ?? deck.UserSubject.Subject!.SubjectName,
                    deck.UserSubject != null && !deck.UserSubject.IsArchived))
                .FirstOrDefaultAsync(cancellationToken);
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

            var subjectId = await RequireSubjectAsync(
                request.UserSubjectId, currentUserId, cancellationToken);
            var now = DateTime.UtcNow;


            var deck = new FlashcardDeck
            {
                OwnerUserId = currentUserId,

                UserSubjectId = subjectId,

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

            var description = request.Description?.Trim();
            if (description?.Length > 2000)
            {
                throw new ArgumentException(
                    "Mô tả không được vượt quá 2000 ký tự", nameof(request.Description));
            }
            deck.Title = title;
            deck.Description = string.IsNullOrWhiteSpace(description) ? null : description;
            deck.UpdatedAtUtc = DateTime.UtcNow;

            repository.Update(deck);

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

        public async Task<IReadOnlyList<FlashcardSubjectOptionDto>> GetSubjectOptionsAsync(
            long currentUserId,
            CancellationToken cancellationToken = default)
        {
            return await repository.Query<UserSubject>()
                .Where(subject => subject.UserId == currentUserId && !subject.IsArchived)
                .OrderBy(subject => subject.CustomSubjectName ?? subject.Subject!.SubjectName)
                .Select(subject => new FlashcardSubjectOptionDto(
                    subject.UserSubjectId,
                    subject.CustomSubjectName ?? subject.Subject!.SubjectName))
                .ToListAsync(cancellationToken);
        }

        private async Task<long> RequireSubjectAsync(
            long? userSubjectId,
            long currentUserId,
            CancellationToken cancellationToken)
        {
            if (userSubjectId is not long id ||
                !await repository.AnyAsync<UserSubject>(
                    subject => subject.UserSubjectId == id &&
                               subject.UserId == currentUserId &&
                               !subject.IsArchived,
                    cancellationToken))
            {
                throw new ArgumentException("Vui lòng chọn môn học hợp lệ của bạn.");
            }

            return id;
        }

    }
}
