using System;
using IfsahApp.Core.Enums;
using IfsahApp.Core.ViewModels;

namespace IfsahApp.Infrastructure.Services;

public static class ErrorService
{
    public static ErrorViewModel GetErrorInfo(int statusCode, IHttpStatusLocalizer localizer, string? requestId = null)
    {
        if (!Enum.IsDefined(typeof(HttpStatusCode), statusCode))
        {
            statusCode = (int)HttpStatusCode.InternalServerError;
        }

        var httpStatus = (HttpStatusCode)statusCode;

        return new ErrorViewModel
        {
            RequestId = requestId ?? string.Empty,
            StatusCode = statusCode,
            ErrorMessage = localizer.GetTitle(httpStatus) ?? "Error",
            ErrorDescription = localizer.GetDescription(httpStatus) ?? string.Empty
        };
    }
}