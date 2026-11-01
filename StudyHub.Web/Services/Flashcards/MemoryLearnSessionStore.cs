using Microsoft.Extensions.Caching.Memory;
using StudyHub.BLL.Services.Flashcards.Interfaces;
using StudyHub.BLL.Services.Flashcards.LearnSessions;


namespace StudyHub.Web.Services.Flashcards
{
    public sealed class MemoryLearnSessionStore(IMemoryCache cache) : ILearnSessionStore
    {
        private static readonly TimeSpan SlidingLifeTime = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan AbsoluteLifeTime = TimeSpan.FromHours(2);
        private static string GetKey(Guid sessionId)
        {
            return $"flashcards:learn-session:{sessionId:N}";
        }

        public void Save(LearnSessionState session)
        {
            ArgumentNullException.ThrowIfNull(session);

            var options = new MemoryCacheEntryOptions
            {
                SlidingExpiration = SlidingLifeTime,
                AbsoluteExpirationRelativeToNow = AbsoluteLifeTime
            };
            cache.Set(
                GetKey(session.SessionId),
                session,
                options);
        }

        public LearnSessionState? Find(
            Guid sessionId,
            long userId,
            long deckId)
        {
            var session = cache.Get<LearnSessionState>(
                GetKey(sessionId));

            if(session is null)
            {
                return null;
            }

            //không cho user khách truy cập vào session
            if(session.UserId != userId || session.DeckId != deckId)
            {
                return null;
            }
            return session;
        }

        public bool Remove(
            Guid sessionId,
            long userId,
            long deckId)
        {
            var session = Find(sessionId, userId, deckId);
            if(session is null)
            {
                return false;
            }

            cache.Remove(GetKey(sessionId));
            return true;
        }
    }
}
