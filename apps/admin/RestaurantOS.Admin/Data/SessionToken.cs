namespace RestaurantOS.Admin.Data;

public sealed class SessionToken
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public string Realm { get; set; } = "platform";
    public string? RoleCode { get; set; }
    public string? Email { get; set; }
}
