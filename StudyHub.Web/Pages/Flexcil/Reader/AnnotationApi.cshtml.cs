using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StudyHub.Web.Data.Entities.Flexcil;
using StudyHub.Web.Enums.Flexcil;
using StudyHub.Web.Services.Flexcil.Annotations;
using System.Text.Json;

namespace StudyHub.Web.Pages.Flexcil.Reader;

public class AnnotationApiModel : PageModel
{
    private readonly IAnnotationService _annotationService;

    public AnnotationApiModel(
        IAnnotationService annotationService)
    {
        _annotationService = annotationService;
    }
    public async Task<IActionResult> OnGetLoadAsync(
    int documentId)
    {
        var userId = "test-user";

        var annotations =
            await _annotationService
                .GetByDocumentAsync(
                    documentId,
                    userId);

        return new JsonResult(
            annotations.Select(x => new
            {
                id = x.Id,
                documentId = x.DocumentId,
                pageNumber = x.PageNumber,
                type = x.Type,
                data = x.Data,
                color = x.Color,
                opacity = x.Opacity,
                strokeWidth = x.StrokeWidth
            })
        );
    }
    public async Task<IActionResult> OnPostSaveAsync(
    [FromBody] SaveAnnotationRequest request)
    {
        if (request == null)
        {
            return BadRequest(new
            {
                success = false,
                message = "Request is null."
            });
        }

        if (request.DocumentId <= 0)
        {
            return BadRequest(new
            {
                success = false,
                message = "Invalid documentId."
            });
        }

        if (request.PageNumber <= 0)
        {
            return BadRequest(new
            {
                success = false,
                message = "Invalid pageNumber."
            });
        }

        if (request.Points == null ||
            request.Points.Count < 2)
        {
            return BadRequest(new
            {
                success = false,
                message = "Annotation must contain points."
            });
        }

        var userId = "test-user";

        var annotation = new Annotation
        {
            DocumentId = request.DocumentId,
            UserId = userId,
            PageNumber = request.PageNumber,
            Type = request.Type,
            Data = JsonSerializer.Serialize(
                request.Points),
            Color = request.Color,
            Opacity = request.Opacity,
            StrokeWidth = request.StrokeWidth
        };

        var result =
            await _annotationService.CreateAsync(
                annotation);

        return new JsonResult(new
        {
            success = true,
            id = result.Id
        });
    }
    public async Task<IActionResult> OnPostDeleteAsync(
    [FromBody] DeleteAnnotationRequest request)
    {
        var userId = "test-user";

        await _annotationService.DeleteAsync(
            request.Id,
            userId);

        return new JsonResult(new
        {
            success = true
        });
    }
}
public class DeleteAnnotationRequest
{
    public int Id { get; set; }
}
public class SaveAnnotationRequest
{
    public int DocumentId { get; set; }

    public int PageNumber { get; set; }

    public AnnotationType Type { get; set; }

    public List<AnnotationPoint> Points { get; set; }
        = new();

    public string Color { get; set; }
        = "#EF4444";

    public double Opacity { get; set; }
        = 1;

    public double StrokeWidth { get; set; }
        = 3;
}

public class AnnotationPoint
{
    public double X { get; set; }

    public double Y { get; set; }
}

