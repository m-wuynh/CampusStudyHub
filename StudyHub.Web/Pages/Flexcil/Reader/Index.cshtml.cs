using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StudyHub.Web.Data.Entities.Flexcil;
using StudyHub.Web.Services.Flexcil.Documents;

namespace StudyHub.Web.Pages.Flexcil.Reader;

public class IndexModel : PageModel
{
    private readonly IDocumentService _documentService;

    public IndexModel(
        IDocumentService documentService)
    {
        _documentService = documentService;
    }

    public Document? Document { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        // Temporary user ID until authentication is integrated.
        var userId = "test-user";

        Document = await _documentService
            .GetByIdAsync(id, userId);

        if (Document == null)
        {
            return NotFound();
        }

        return Page();
    }
}
