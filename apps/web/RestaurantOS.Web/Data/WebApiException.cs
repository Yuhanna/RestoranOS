namespace RestaurantOS.Web.Data;

public sealed class WebApiException : Exception
{
    public WebApiException(int statusCode, ErrorResponse? error, string? rawBody = null)
        : base(error?.Detail ?? error?.Title ?? rawBody ?? $"API request failed ({statusCode}).")
    {
        StatusCode = statusCode;
        Error = error;
        RawBody = rawBody;
    }

    public int StatusCode { get; }

    public ErrorResponse? Error { get; }

    public string? RawBody { get; }
}
