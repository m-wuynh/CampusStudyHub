namespace StudyHub.DAL.Repositories.Common;

public sealed class DataConflictException(Exception innerException)
    : Exception("Dữ liệu vừa thay đổi hoặc đã tồn tại. Hãy tải lại và thử lại.", innerException);
