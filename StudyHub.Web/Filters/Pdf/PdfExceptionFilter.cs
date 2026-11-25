using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using StudyHub.BLL.Services.Pdf;

namespace StudyHub.Web.Filters.Pdf;

public sealed class PdfExceptionFilter(IModelMetadataProvider metadataProvider) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not PdfException exception)
        {
            return;
        }

        var result = new ViewResult
        {
            ViewName = "~/Views/Pdf/Error.cshtml",
            StatusCode = exception.StatusCode,
            ViewData = new ViewDataDictionary(metadataProvider, context.ModelState)
        };

        result.ViewData["Status"] = exception.StatusCode;
        result.ViewData["Message"] = exception.Message;

        context.Result = result;
        context.ExceptionHandled = true;
    }
}
