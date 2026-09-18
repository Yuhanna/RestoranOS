using System.Text.Json;
using RestaurantOS.Application;
using RestaurantOS.Domain;

namespace RestaurantOS.Api.Infrastructure;

internal static class IncidentCapture
{
    public static void FromCustomerException(
        IIncidentRecorder recorder,
        HttpContext http,
        CustomerExperienceException exception,
        int httpStatus,
        CustomerClientContext? client = null,
        string? sessionToken = null,
        object? detail = null)
    {
        recorder.Record(new IncidentDraft(
            Channel: IncidentChannels.Customer,
            Code: exception.Code,
            Message: exception.Message,
            ActorType: string.IsNullOrWhiteSpace(sessionToken)
                ? IncidentActorTypes.Anonymous
                : IncidentActorTypes.Guest,
            HttpStatus: httpStatus,
            CorrelationId: http.TraceIdentifier,
            ClientDeviceId: client?.DeviceId,
            ClientIp: client?.ClientIp ?? http.Connection.RemoteIpAddress?.ToString(),
            RequestMethod: http.Request.Method,
            RequestPath: http.Request.Path.Value,
            DetailJson: detail is null ? null : JsonSerializer.Serialize(detail),
            SessionTokenForLookup: sessionToken));
    }

    public static void FromUnhandled(
        IIncidentRecorder recorder,
        HttpContext http,
        Exception exception)
    {
        recorder.Record(new IncidentDraft(
            Channel: IncidentChannels.System,
            Code: "UNHANDLED_EXCEPTION",
            Message: exception.GetType().Name + ": " + Truncate(exception.Message, 400),
            ActorType: IncidentActorTypes.System,
            Severity: IncidentSeverities.Critical,
            HttpStatus: StatusCodes.Status500InternalServerError,
            CorrelationId: http.TraceIdentifier,
            ClientIp: http.Connection.RemoteIpAddress?.ToString(),
            RequestMethod: http.Request.Method,
            RequestPath: http.Request.Path.Value,
            DetailJson: JsonSerializer.Serialize(new
            {
                exceptionType = exception.GetType().FullName,
            })));
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
