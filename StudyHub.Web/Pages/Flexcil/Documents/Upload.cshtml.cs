using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StudyHub.Web.Services.Flexcil.Documents;

namespace StudyHub.Web.Pages.Flexcil.Documents;

public class UploadModel : PageModel
{
    private readonly IDocumentService _documentService;

    public UploadModel(
        IDocumentService documentService)
    {
        _documentService = documentService;
    }

    [BindProperty]
    public IFormFile? PdfFile { get; set; }

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            if (PdfFile == null)
            {
                ErrorMessage = "Please select a PDF file.";
                return Page();
            }

            // Temporary user ID.
            // Authentication will replace this later.
            var userId = "test-user";

            await _documentService.UploadAsync(
                PdfFile,
                userId);

            return RedirectToPage("./Index");
        }
        catch (ArgumentException ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
    }
}
