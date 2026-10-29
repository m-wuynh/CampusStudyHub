using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace StudyHub.Web.Configuration;

public sealed class DisableLegacyNotesControllers
    : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        const string legacyNamespace =
            "StudyHub.Web.Controllers.Notes";

        for (var index = application.Controllers.Count - 1;
             index >= 0;
             index--)
        {
            var controllerNamespace =
                application.Controllers[index]
                    .ControllerType.Namespace;

            if (controllerNamespace == legacyNamespace ||
                controllerNamespace?.StartsWith(
                    legacyNamespace + ".",
                    StringComparison.Ordinal) == true)
            {
                application.Controllers.RemoveAt(index);
            }
        }
    }
}