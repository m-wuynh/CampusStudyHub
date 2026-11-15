using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using StudyHub.BLL.Services.Pdf;
using StudyHub.Web.ViewModels.Pdf;

namespace StudyHub.Web.Filters.Pdf;

public sealed class PdfExceptionFilter(
    IModelMetadataProvider metadataProvider) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not PdfException exception)
        {
            return;
        }

        var problem = new ProblemDetails
        {
            Status = exception.StatusCode,
            Title = exception.Message,
            Instance = context.HttpContext.Request.Path.Value
        };

        if (ExpectsJson(context.HttpContext.Request))
        {
            var result = new ObjectResult(problem)
            {
                StatusCode = exception.StatusCode
            };

            result.ContentTypes.Add("application/problem+json");
            context.Result = result;
        }
        else
        {
            context.Result = new ViewResult
            {
                ViewName = "~/Views/Pdf/Error.cshtml",
                StatusCode = exception.StatusCode,
                ViewData = new ViewDataDictionary<PdfErrorViewModel>(
                    metadataProvider,
                    context.ModelState)
                {
                    Model = new PdfErrorViewModel(
                        exception.StatusCode,
                        exception.Message)
                }
            };
        }

        context.ExceptionHandled = true;
    }

    private static bool ExpectsJson(HttpRequest request) =>
        string.Equals(
            request.Headers.XRequestedWith,
            "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase) ||
        request.Headers.Accept.Any(value =>
            value?.Contains(
                "application/json",
                StringComparison.OrdinalIgnoreCase) == true ||
            value?.Contains(
                "application/problem+json",
                StringComparison.OrdinalIgnoreCase) == true);
}
