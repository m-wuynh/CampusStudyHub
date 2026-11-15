using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StudyHub.BLL.Services.NotesV3;

namespace StudyHub.Web.Filters;

public sealed class NotesV3ExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not NotesV3Exception exception)
        {
            return;
        }

        var problem = new ProblemDetails
        {
            Status = exception.StatusCode,
            Title = exception.Message,
            Instance = context.HttpContext.Request.Path.Value
        };

        var result = new ObjectResult(problem)
        {
            StatusCode = exception.StatusCode
        };

        result.ContentTypes.Add("application/problem+json");

        context.Result = result;
        context.ExceptionHandled = true;
    }
}