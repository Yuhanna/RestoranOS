using Microsoft.AspNetCore.Diagnostics;
using RestaurantOS.Api.Infrastructure;
using RestaurantOS.Application;

namespace RestaurantOS.Api;

/// <summary>Records unhandled exceptions as operational incidents (does not replace ProblemDetails).</summary>
public sealed class UnhandledExceptionIncidentHandler(IIncidentRecorder recorder) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        IncidentCapture.FromUnhandled(recorder, httpContext, exception);
        return ValueTask.FromResult(false);
    }
}
