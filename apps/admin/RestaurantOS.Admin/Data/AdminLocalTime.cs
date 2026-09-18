using System.Globalization;

namespace RestaurantOS.Admin.Data;

public static class AdminLocalTime
{
    public static string? ToInput(DateTimeOffset? utc) =>
        utc?.ToLocalTime().ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);

    public static bool TryParseOptional(string? localText, out DateTimeOffset? utc, out string error)
    {
        utc = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(localText))
        {
            return true;
        }

        if (!DateTime.TryParse(localText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)
            && !DateTime.TryParse(localText, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out local))
        {
            error = "Tarih geçersiz.";
            return false;
        }

        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        var offset = new DateTimeOffset(unspecified, TimeZoneInfo.Local.GetUtcOffset(unspecified));
        utc = offset.ToUniversalTime();
        return true;
    }
}
