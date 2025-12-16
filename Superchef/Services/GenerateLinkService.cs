namespace Superchef.Services;

public class GenerateLinkService
{
    private readonly IHttpContextAccessor ct;
    private readonly LinkGenerator lg;

    // Inject LinkGenerator
    public GenerateLinkService(IHttpContextAccessor ct, LinkGenerator lg)
    {
        this.ct = ct;
        this.lg = lg;
    }

    public string GetPageLink(string action, string controller, string page)
    {
        var httpContext = ct.HttpContext;
        if (httpContext == null) return "";

        // Clone existing query parameters
        var routeValues = new RouteValueDictionary();

        foreach (var key in httpContext.Request.Query.Keys)
        {
            routeValues[key] = httpContext.Request.Query[key];
        }

        // Set Page
        routeValues["Page"] = page;

        var path = lg.GetPathByAction(
            httpContext,
            action: action,
            controller: controller,
            values: routeValues
        );

        return path ?? "";
    }

    public string GetSortLink(string action, string controller, string field, string currentSort, string currentDir)
    {
        var httpContext = ct.HttpContext;
        if (httpContext == null) return "";

        var dir = "asc";
        if (field == currentSort)
        {
            dir = currentDir == "desc" ? "asc" : "desc";
        }

        // Clone existing query parameters
        var routeValues = new RouteValueDictionary();

        foreach (var key in httpContext.Request.Query.Keys)
        {
            routeValues[key] = httpContext.Request.Query[key];
        }

        // Set Sort & Dir
        routeValues["Sort"] = field;
        routeValues["Dir"] = dir;

        var path = lg.GetPathByAction(
            httpContext,
            action: action,
            controller: controller,
            values: routeValues
        );

        return path ?? "";
    }
}