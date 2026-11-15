using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StudyHub.BLL.Services.Flashcards.LearnSessions;

namespace StudyHub.BLL.Services.Flashcards.Interfaces
{
    public interface ILearnSessionStore
    {
        void Save(LearnSessionState session);

        LearnSessionState? Find(
            Guid sessionId,
            long userId,
            long deckId);

        bool Remove(
            Guid sessionId,
            long userId,
            long deckId);
    }
}
