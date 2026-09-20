using Microsoft.AspNetCore.Mvc.RazorPages;
using StudyHub.Web.Data.Entities.Flexcil;
using StudyHub.Web.Services.Flexcil.Documents;

namespace StudyHub.Web.Pages.Flexcil.Documents;

public class IndexModel : PageModel
{
    private readonly IDocumentService _documentService;

    public IndexModel(
        IDocumentService documentService)
    {
        _documentService = documentService;
    }

    public List<Document> Documents { get; set; } = [];

    public async Task OnGetAsync()
    {
        var userId = "test-user";

        Documents = await _documentService
            .GetByUserAsync(userId);
    }
}
