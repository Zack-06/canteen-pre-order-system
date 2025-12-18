namespace Superchef.Middlewares;

public class ExpiryCleanupMiddleware
{
    private RequestDelegate next;
    public ExpiryCleanupMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public async Task Invoke(HttpContext context, CleanupService clnSrv)
    {
        await clnSrv.ExpiryCleanup();
        await next(context);
    }
}