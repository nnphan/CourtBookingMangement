using Serilog.Context;

namespace CourtBookingManagement.Api.Common.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var requestHeader)
            && Guid.TryParse(requestHeader.ToString(), out var suppliedId)
                ? suppliedId.ToString("D")
                : Guid.NewGuid().ToString("D");

        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
