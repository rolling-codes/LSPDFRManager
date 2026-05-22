using System.Net;

namespace LSPDFRManager.LocalApi.Middleware;

public class LocalhostOnlyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress;
        if (ip is null || !IPAddress.IsLoopback(ip))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Forbidden: local access only.");
            return;
        }

        await next(context);
    }
}
