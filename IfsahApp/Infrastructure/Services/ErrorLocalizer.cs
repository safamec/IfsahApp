using Microsoft.Extensions.Localization;
using IfsahApp.Core.Enums;

namespace IfsahApp.Infrastructure.Services;

public interface IHttpStatusLocalizer
{
    string GetTitle(HttpStatusCode status);
    string GetDescription(HttpStatusCode status);
}

public class ErrorLocalizer : IHttpStatusLocalizer
{
    private readonly IStringLocalizer _localizer;

    public ErrorLocalizer(IStringLocalizerFactory factory)
    {
        var assemblyName = typeof(ErrorLocalizer).Assembly.GetName().Name!;
        
        // ✅ المسار الصحيح: "IfsahApp.Resources.ErrorMessages"
        var resourceBaseName = "ErrorMessages";
        
        _localizer = factory.Create(resourceBaseName, assemblyName);
    }

    public string GetTitle(HttpStatusCode status)
    {
        var key = $"{status}_Title";
        var localized = _localizer[key];
        
        if (!localized.ResourceNotFound) 
            return localized.Value;

        // Fallback إلى القيمة الافتراضية
        return _localizer[$"{status}_Title"] ?? "Error";
    }

    public string GetDescription(HttpStatusCode status)
    {
        var key = $"{status}_Description";
        var localized = _localizer[key];
        
        if (!localized.ResourceNotFound) 
            return localized.Value;

        // Fallback إلى القيمة الافتراضية
        return _localizer[$"{status}_Description"] ?? "";
    }
}