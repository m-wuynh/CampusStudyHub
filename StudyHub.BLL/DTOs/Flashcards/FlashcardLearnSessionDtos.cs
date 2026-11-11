using StudyHub.DAL.Enums.Flashcards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyHub.BLL.DTOs.Flashcards
{
    public sealed record LearnCardSnapshotDto(
        long CardId,
        LearnCardStatus Status,
        int CorrrectCount,
        int IncorrectCount);

    public sealed record LearnSessionInfoDto(
        Guid SessionId,
        long DeckId,
        int TotalCards,
        int NewCount,
        int LearningCount,
        int LearnedCount,
        bool IsCompleted,
        bool ShuffleEnabled = false);

    public sealed record LearnSessionQuestionDto(
        Guid SessionId,
        Guid QuestionToken,
        FlashcardLearnQuestionDto Question);

    public sealed record LearnSessionAnswerDto(
        FlashcardLearnAnswerResultDto Answer,
        LearnCardSnapshotDto Card,
        LearnSessionInfoDto Session);

    public sealed record DbLearnStateDto(
        LearnSessionInfoDto Session,
        long? NextCardId);

    public sealed record LearnResultDto(
    Guid SessionId,
    long DeckId,
    int TotalAttempts,
    int Correct,
    int Incorrect,
    double Accuracy,
    int TotalCards,
    int Learned,
    int Remaining,
    bool IsCompleted);

}
