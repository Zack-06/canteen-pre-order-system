using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Superchef;

public static class Extensions
{
    public static bool IsAjax(this HttpRequest request)
    {
        return request.Headers.XRequestedWith == "XMLHttpRequest";
    }

    public static bool IsValid(this ModelStateDictionary ms, string key)
    {
        return ms.GetFieldValidationState(key) == ModelValidationState.Valid;
    }

    public static Account? GetAccount(this HttpContext context)
    {
        return context.Items["Account"] as Account;
    }

    public static int? GetDeviceId(this HttpContext context)
    {
        return context.Items["DeviceId"] as int?;
    }

    public static string GetBaseUrl(this HttpRequest request)
    {
        return $"{request.Scheme}://{request.Host}";
    }
}
