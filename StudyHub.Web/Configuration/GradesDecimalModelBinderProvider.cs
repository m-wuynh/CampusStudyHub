using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;

namespace StudyHub.Web.Configuration;

/// <summary>
/// Form của GradesController luôn gửi số theo dạng "8.5" (input type=number). Binder mặc định đọc số
/// theo culture của server (vi-VN hiểu "8.5" thành 85), nên với controller Grades số được đọc theo
/// InvariantCulture; dấu phẩy thập phân "8,5" cũng được chấp nhận. Controller khác giữ nguyên binder mặc định.
/// </summary>
public sealed class GradesDecimalModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        if (context.Metadata.UnderlyingOrModelType != typeof(decimal)) return null;

        var loggerFactory = context.Services.GetRequiredService<ILoggerFactory>();
        var fallback = new DecimalModelBinder(NumberStyles.Float | NumberStyles.AllowThousands, loggerFactory);
        return new GradesDecimalModelBinder(fallback);
    }

    private sealed class GradesDecimalModelBinder(IModelBinder fallback) : IModelBinder
    {
        private const string GradesController = "Grades";
        private const NumberStyles Styles =
            NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite |
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var controller = bindingContext.ActionContext.RouteData.Values["controller"] as string;
            if (!string.Equals(controller, GradesController, StringComparison.OrdinalIgnoreCase))
                return fallback.BindModelAsync(bindingContext);

            var valueResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            if (valueResult == ValueProviderResult.None) return Task.CompletedTask;

            bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueResult);

            var raw = valueResult.FirstValue?.Trim();
            if (string.IsNullOrEmpty(raw)) return Task.CompletedTask; // để trống: nullable → null, non-nullable → giá trị mặc định

            if (decimal.TryParse(raw.Replace(',', '.'), Styles, CultureInfo.InvariantCulture, out var value))
                bindingContext.Result = ModelBindingResult.Success(value);
            else
                bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, $"Giá trị số '{raw}' không hợp lệ.");

            return Task.CompletedTask;
        }
    }
}
