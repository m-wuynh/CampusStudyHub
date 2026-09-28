using System.ComponentModel.DataAnnotations;

namespace StudyHub.BLL.DTOs.Notes;

public sealed class NotebookPageDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "Trang 1";
    public string Body { get; set; } = "";
}

public sealed class NotebookEditDto
{
    public long? Id { get; set; }

    [Required, StringLength(200)]
    public string Title { get; set; } = "Cuốn ghi chú mới";

    public string Format { get; set; } = "PlainText";
    public string? Version { get; set; }
    public Guid PageId { get; set; }

    [Required, StringLength(100)]
    public string PageTitle { get; set; } = "Trang 1";

    [StringLength(200000)]
    public string Body { get; set; } = "";

    public List<NotebookPageDto> Pages { get; set; } = [];
}

public sealed record NotebookSaveResult(long Id, Guid PageId);
