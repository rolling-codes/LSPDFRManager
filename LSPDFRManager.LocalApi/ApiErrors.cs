using LSPDFRManager.Core;

namespace LSPDFRManager.LocalApi;

/// <summary>
/// Centralized 500-level error responses. Logs the full exception server-side
/// with a short correlation id and returns only a generic message to the
/// client, so internal detail — absolute file paths, stack traces — never
/// leaks over the API. Endpoints pass a human context string, not ex.Message.
/// </summary>
public static class ApiErrors
{
    public static IResult Problem(string context, Exception ex)
    {
        var correlationId = Guid.NewGuid().ToString("N")[..8];
        AppLogger.Error($"[API_ERROR {correlationId}] {context}", ex);
        return Results.Problem(
            detail: $"{context}. Reference id: {correlationId} (see app.log for details).",
            statusCode: StatusCodes.Status500InternalServerError);
    }
}
