using System.Globalization;

namespace RestaurantOS.Admin.Data;

public static class AdminAccess
{
    public static bool CanWriteCatalog(string? roleCode) =>
        roleCode is "Owner" or "Billing";

    public static bool CanPublishCatalog(string? roleCode) =>
        roleCode is "Owner";

    public static bool CanArchiveCatalog(string? roleCode, string status) =>
        status switch
        {
            "draft" => CanWriteCatalog(roleCode),
            "published" => CanPublishCatalog(roleCode),
            _ => false,
        };

    public static bool CanWriteCampaigns(string? roleCode) =>
        roleCode is "Owner" or "Billing" or "Support";

    public static bool CanWriteNotifications(string? roleCode) =>
        CanWriteCampaigns(roleCode);

    public static bool CanManageStaff(string? roleCode) =>
        roleCode is "Owner";

    public static bool CanWriteTenants(string? roleCode) =>
        CanWriteCatalog(roleCode);

    public static bool CanAcceptQuotes(string? roleCode) =>
        CanWriteCatalog(roleCode);

    public static bool CanRejectQuotes(string? roleCode) =>
        CanWriteCampaigns(roleCode);

    public static string QuoteStatusLabel(string? status) => status switch
    {
        "Open" => "Açık",
        "Accepted" => "Onaylandı",
        "Rejected" => "Reddedildi",
        _ => string.IsNullOrWhiteSpace(status) ? "—" : status,
    };

    public static string LimitLabel(int? value) =>
        value is null ? "sınırsız" : value.Value.ToString("N0", CultureInfo.CurrentCulture);

    public static string HoursLabel(int hours)
    {
        if (hours % 24 == 0)
        {
            return string.Create(CultureInfo.CurrentCulture, $"{hours / 24} gün");
        }

        return string.Create(CultureInfo.CurrentCulture, $"{hours} saat");
    }

    public static string AudienceLabel(string? audience) => audience switch
    {
        "non_pro" => "Pro olmayanlar",
        "free" => "Free",
        "trial" => "Deneme",
        "pro" => "Pro",
        "all" => "Tümü",
        _ => string.IsNullOrWhiteSpace(audience) ? "—" : audience,
    };

    public static string StaffRoleLabel(string? roleCode) => roleCode switch
    {
        "Owner" => "Owner",
        "Billing" => "Billing",
        "Support" => "Support",
        "ReadOnly" => "ReadOnly",
        _ => roleCode ?? "—",
    };
}
