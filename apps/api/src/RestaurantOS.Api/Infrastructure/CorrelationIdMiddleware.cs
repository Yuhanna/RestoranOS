using Microsoft.Extensions.Primitives;

namespace RestaurantOS.Api.Infrastructure;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context.Request.Headers[HeaderName]);
        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }

    private static string GetOrCreateCorrelationId(StringValues headerValues)
    {
        var candidate = headerValues.FirstOrDefault();

        return !string.IsNullOrWhiteSpace(candidate)
            && candidate.Length <= 128
            && candidate.All(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            ? candidate
            : Guid.NewGuid().ToString("N");
    }
}
