namespace RestaurantOS.Web.Data;

public sealed class JwtToken
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
}
