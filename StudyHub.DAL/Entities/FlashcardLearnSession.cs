using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyHub.DAL.Entities
{
    public partial class FlashcardLearnSession
    {
        public Guid SessionId { get; set; }

        public bool ShuffleEnabled { get; set; }
        public long UserId { get; set; }
        public long DeckId { get; set; }

        public DateTime StartedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }

        public DateTime? InvalidatedAtUtc { get; set; }

        public long? CurrentCardId { get; set; }
        public Guid? QuestionToken { get; set; }
        public string? QuestionOptionsJson { get; set; }

        public byte[] RowVersion { get; set; } = null!;

        public virtual ICollection<FlashcardReview> Reviews { get; set; }
            = new List<FlashcardReview>();
    }
}
