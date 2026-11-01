using StudyHub.BLL.DTOs.Flashcards;
using StudyHub.DAL.Enums.Flashcards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyHub.BLL.Services.Flashcards.LearnSessions
{
    public sealed class LearnCardState
    {
        public long CardId { get; }

        public LearnCardStatus Status { get; private set; } = LearnCardStatus.New;

        public int CorrectCount { get; private set; }

        public int IncorrectCount { get; private set; }

        public LearnCardState(long cardId)
        {
            if(cardId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cardId));
            }

            CardId = cardId;
        }

        internal void ApplyGradedAnswer(bool isCorrect)
        {
            if(Status == LearnCardStatus.Learned)
            {
                throw new InvalidOperationException(
                    "Card đã hoàn thành trong session");
            }
            if (isCorrect)
            {
                CorrectCount++;
            }
            else
            {
                IncorrectCount++;
            }

            Status = CorrectCount >= 2 ? LearnCardStatus.Learned : LearnCardStatus.Learning;
        }

        internal LearnCardSnapshotDto ToSnapshot()
        {
            return new LearnCardSnapshotDto(
                CardId,
                Status,
                CorrectCount,
                IncorrectCount);
        }
    }
}
