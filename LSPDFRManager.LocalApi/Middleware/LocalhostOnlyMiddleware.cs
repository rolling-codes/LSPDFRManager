namespace LSPDFRManager.LocalApi.Middleware;

public class LocalhostOnlyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var host = context.Request.Host.Host;
        if (host is not ("localhost" or "127.0.0.1"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Forbidden: local access only.");
            return;
        }

        await next(context);
    }
}
