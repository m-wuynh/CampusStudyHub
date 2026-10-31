// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;

// namespace StudyHub.Web.Controllers.NotesV3;

// [Authorize]
// [Route("Notes")]
// public sealed class LegacyNotesRedirectController : Controller
// {
//     [HttpGet("")]
//     public IActionResult Index()
//     {
//         return RedirectToAction(
//             "Index",
//             "NotesV3");
//     }

//     [HttpGet("{**path}")]
//     public IActionResult Retired(string? path)
//     {
//         return StatusCode(
//             StatusCodes.Status410Gone,
//             "Đường dẫn Notes/PDF cũ đã ngừng sử dụng. " +
//             "Hãy mở Ghi chú & PDF từ menu.");
//     }
// }