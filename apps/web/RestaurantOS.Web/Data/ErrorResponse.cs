namespace RestaurantOS.Web.Data;

public sealed class ErrorResponse
{
    public string? Title { get; set; }
    public string? Detail { get; set; }
    public string? Code { get; set; }
    public int? Status { get; set; }
}
