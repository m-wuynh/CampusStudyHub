using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace StudyHub.Web.Pages;

public class LoginModel : PageModel
{
    [BindProperty]
    public string? Email { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    [BindProperty]
    public bool RememberMe { get; set; }

    public void OnGet() { }

    public IActionResult OnPost()
    {
        // TODO: Implement real authentication logic here
        // For demo: redirect to dashboard
        return RedirectToPage("/Index");
    }
}
